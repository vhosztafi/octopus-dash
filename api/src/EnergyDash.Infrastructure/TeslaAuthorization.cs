using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EnergyDash.Infrastructure;

public sealed record TeslaAppCredentials(string ClientId, string ClientSecret);
public sealed record TeslaAuthorizationStart(string State, string AuthorizationUrl);
public sealed record TeslaAuthorizationCallback(string State, string Code);

public sealed class TeslaAuthorization(HttpClient http, SecretStore secrets, EnergyDbContext db, IConfiguration configuration)
{
    public const string Origin = "https://octopus-dash.vilmos.co.uk";
    public const string RedirectUri = Origin + "/auth/tesla/callback";
    public const string Scopes = "openid offline_access energy_device_data";
    private const string PendingKey = "Tesla__OAuthPending";
    private const string TokenUrl = "https://fleet-auth.prd.vn.cloud.tesla.com/oauth2/v3/token";
    private string Audience => configuration["Tesla:Region"] == "na" ? "https://fleet-api.prd.na.vn.cloud.tesla.com" : "https://fleet-api.prd.eu.vn.cloud.tesla.com";
    private sealed record Pending(string State, string ClientId, string ClientSecret, DateTimeOffset Expires);

    private async Task LockAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'EnergyDash.TeslaToken', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; IF @result < 0 THROW 50000, 'Tesla authorization lock unavailable', 1;", ct);
        db.ChangeTracker.Clear();
    }

    public async Task<TeslaAuthorizationStart> BeginAsync(TeslaAppCredentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClientId) || credentials.ClientId.Length > 500 ||
            string.IsNullOrWhiteSpace(credentials.ClientSecret) || credentials.ClientSecret.Length > 2000)
            throw new ArgumentException("Provide your Tesla developer app client ID and secret.");
        if (new[] { "Tesla:ClientId", "Tesla:ClientSecret", "Tesla:RefreshToken" }.Any(k => !string.IsNullOrWhiteSpace(configuration[k])))
            throw new ArgumentException("Server-managed Tesla credentials must be removed before browser authorization.");
        // Registration establishes only this application's domain; owner consent follows separately.
        using var partner = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret, ["audience"] = Audience, ["scope"] = "energy_device_data"
        }), ct);
        partner.EnsureSuccessStatusCode();
        var partnerToken = await partner.Content.ReadFromJsonAsync<JsonElement>(ct);
        using var register = new HttpRequestMessage(HttpMethod.Post, Audience + "/api/1/partner_accounts")
        { Content = JsonContent.Create(new { domain = new Uri(Origin).Host }) };
        register.Headers.Authorization = new AuthenticationHeaderValue("Bearer", partnerToken.GetProperty("access_token").GetString());
        using var registration = await http.SendAsync(register, ct);
        registration.EnsureSuccessStatusCode();

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await secrets.PutAsync(PendingKey, JsonSerializer.Serialize(new Pending(state, credentials.ClientId, credentials.ClientSecret, DateTimeOffset.UtcNow.AddMinutes(15))), ct);
        await tx.CommitAsync(ct);
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = credentials.ClientId, ["redirect_uri"] = RedirectUri,
            ["scope"] = Scopes, ["state"] = state, ["require_requested_scopes"] = "true"
        };
        return new(state, "https://auth.tesla.com/oauth2/v3/authorize?" + string.Join("&", query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))));
    }

    public async Task CompleteAsync(TeslaAuthorizationCallback callback, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(callback.State) || callback.State.Length != 64 || !callback.State.All(Uri.IsHexDigit) || callback.Code is null || callback.Code.Length > 5000)
            throw new ArgumentException("Invalid Tesla authorization response.");
        Pending pending;
        await using (var claim = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAsync(ct);
            var value = await secrets.GetStoredAsync(PendingKey, ct);
            pending = (string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<Pending>(value))
                ?? throw new ArgumentException("Tesla authorization expired or was already used.");
            if (pending.Expires <= DateTimeOffset.UtcNow || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pending.State), Encoding.UTF8.GetBytes(callback.State)))
                throw new ArgumentException("Tesla authorization state does not match.");
            // Commit consumption before any single-use exchange. An ambiguous failure requires a fresh authorization.
            await secrets.PutAsync(PendingKey, "", ct);
            await claim.CommitAsync(ct);
        }
        if (string.IsNullOrEmpty(callback.Code)) return; // Owner cancelled consent.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        using var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = pending.ClientId,
            ["client_secret"] = pending.ClientSecret, ["code"] = callback.Code,
            ["audience"] = Audience, ["redirect_uri"] = RedirectUri
        }), ct);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TeslaToken>(ct);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken) || token.ExpiresIn <= 0)
            throw new InvalidOperationException("Tesla did not provide renewable authorization.");
        await secrets.PutAsync("Tesla__ClientId", pending.ClientId, ct);
        await secrets.PutAsync("Tesla__ClientSecret", pending.ClientSecret, ct);
        await secrets.PutAsync("Tesla__AccessToken", token.AccessToken, ct);
        await secrets.PutAsync("Tesla__RefreshToken", token.RefreshToken, ct);
        await secrets.PutAsync("Tesla__Expires", DateTime.UtcNow.AddSeconds(token.ExpiresIn).ToString("O"), ct);
        await tx.CommitAsync(ct);
    }
}

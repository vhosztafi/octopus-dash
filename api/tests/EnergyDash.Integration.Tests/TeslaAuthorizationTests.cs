using System.Net;
using System.Text.Json;
using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class TeslaAuthorizationTests
{
    private sealed class TeslaHandler : HttpMessageHandler
    {
        public int CodeExchanges { get; private set; }
        public bool FailExchange { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/1/partner_accounts")
            {
                Assert.Equal("https://fleet-api.prd.eu.vn.cloud.tesla.com", request.RequestUri.GetLeftPart(UriPartial.Authority));
                Assert.Contains("octopus-dash.vilmos.co.uk", await request.Content!.ReadAsStringAsync(ct));
                return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }
            Assert.Equal("https://fleet-auth.prd.vn.cloud.tesla.com/oauth2/v3/token", request.RequestUri.AbsoluteUri);
            var form = await request.Content!.ReadAsStringAsync(ct);
            if (form.Contains("grant_type=client_credentials", StringComparison.Ordinal))
            {
                Assert.Contains("energy_device_data", form); Assert.DoesNotContain("vehicle_cmds", form); Assert.DoesNotContain("energy_cmds", form);
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"synthetic-partner-token\"}") };
            }
            CodeExchanges++;
            Assert.Contains("grant_type=authorization_code", form);
            Assert.Contains(Uri.EscapeDataString(TeslaAuthorization.RedirectUri), form);
            if (FailExchange) return new(HttpStatusCode.BadRequest) { Content = new StringContent("synthetic failure") };
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"synthetic-new-access\",\"refresh_token\":\"synthetic-new-refresh\",\"expires_in\":3600}") };
        }
    }

    [SqlFact]
    public async Task EncryptedAuthorizationConsumesStateAndPreservesCredentialsOnFailure()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build(); await sql.StartAsync();
        await using var db = new EnergyDbContext(new DbContextOptionsBuilder<EnergyDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().Build();
        var secrets = new SecretStore(db, new EphemeralDataProtectionProvider(), config);
        var handler = new TeslaHandler(); using var http = new HttpClient(handler);
        var oauth = new TeslaAuthorization(http, secrets, db, config);
        await secrets.PutAsync("Tesla__RefreshToken", "synthetic-old-refresh");
        var app = new TeslaAppCredentials("synthetic-app-id", "synthetic-private-client-secret");
        var start = await oauth.BeginAsync(app, default);
        Assert.Contains("scope=openid%20offline_access%20energy_device_data", start.AuthorizationUrl);
        Assert.DoesNotContain(app.ClientSecret, start.AuthorizationUrl);
        var stored = await db.Set<EnergyDash.Core.Secret>().SingleAsync(s => s.Key == "Tesla__OAuthPending");
        Assert.DoesNotContain(app.ClientSecret, stored.Protected);
        await Assert.ThrowsAsync<ArgumentException>(() => oauth.CompleteAsync(new(new string('0', 64), "synthetic-code"), default));
        Assert.Equal(0, handler.CodeExchanges);
        await oauth.CompleteAsync(new(start.State, "synthetic-code"), default);
        Assert.Equal("synthetic-new-refresh", await secrets.GetStoredAsync("Tesla__RefreshToken"));
        Assert.Equal("synthetic-new-access", await secrets.GetStoredAsync("Tesla__AccessToken"));
        await Assert.ThrowsAsync<ArgumentException>(() => oauth.CompleteAsync(new(start.State, "synthetic-code"), default));
        Assert.Equal(1, handler.CodeExchanges);

        var cancelled = await oauth.BeginAsync(app, default);
        await oauth.CompleteAsync(new(cancelled.State, ""), default);
        Assert.Equal(1, handler.CodeExchanges);
        Assert.Equal("synthetic-new-refresh", await secrets.GetStoredAsync("Tesla__RefreshToken"));

        var failed = await oauth.BeginAsync(app, default); handler.FailExchange = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => oauth.CompleteAsync(new(failed.State, "synthetic-bad-code"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => oauth.CompleteAsync(new(failed.State, "synthetic-bad-code"), default));
        Assert.Equal(2, handler.CodeExchanges);
        Assert.Equal("synthetic-new-refresh", await secrets.GetStoredAsync("Tesla__RefreshToken"));

        var expired = await oauth.BeginAsync(app, default);
        using var pending = JsonDocument.Parse((await secrets.GetStoredAsync("Tesla__OAuthPending"))!);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(pending.RootElement.GetRawText())!;
        values["Expires"] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.AddMinutes(-1));
        await secrets.PutAsync("Tesla__OAuthPending", JsonSerializer.Serialize(values));
        await Assert.ThrowsAsync<ArgumentException>(() => oauth.CompleteAsync(new(expired.State, "synthetic-code"), default));
        Assert.Equal(2, handler.CodeExchanges);
    }
}

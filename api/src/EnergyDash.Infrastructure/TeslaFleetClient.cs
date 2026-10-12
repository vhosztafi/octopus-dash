using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EnergyDash.Infrastructure;

public sealed record TeslaSite([property: JsonPropertyName("energy_site_id")] long? EnergySiteId, string? SiteName);
public sealed record TeslaLive([property: JsonPropertyName("percentage_charged")] decimal PercentageCharged, [property: JsonPropertyName("grid_power")] decimal GridPower, [property: JsonPropertyName("battery_power")] decimal BatteryPower, [property: JsonPropertyName("load_power")] decimal LoadPower, [property: JsonPropertyName("solar_power")] decimal SolarPower, [property: JsonPropertyName("grid_status")] string? GridStatus);
public sealed record BatteryLive(TeslaLive Status, decimal Reserve, DateTime SyncedUtc);
public interface IBatteryProvider { Task<IReadOnlyList<TeslaSite>> SitesAsync(CancellationToken ct); Task<BatteryLive> LiveAsync(string siteId, CancellationToken ct); Task<IReadOnlyList<BatteryInterval>> HistoryAsync(string siteId, DateTime start, DateTime end, CancellationToken ct); }
public interface ILiveImportSource { Task<PowerSample?> SampleAsync(CancellationToken ct); }
public sealed record TeslaToken([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("refresh_token")] string RefreshToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed class TeslaFleetClient(HttpClient http, SecretStore secrets, EnergyDbContext db, IConfiguration configuration) : IBatteryProvider
{
    private string BaseUrl => configuration["Tesla:Region"] == "na" ? "https://fleet-api.prd.na.vn.cloud.tesla.com" : "https://fleet-api.prd.eu.vn.cloud.tesla.com";
    private async Task<string> TokenAsync(CancellationToken ct)
    {
        // Refresh tokens are single-use: serialize refresh across API and worker using SQL.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'EnergyDash.TeslaToken', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; IF @result < 0 THROW 50000, 'Tesla token refresh lock unavailable', 1;", ct);
        db.ChangeTracker.Clear();
        var access = await secrets.GetStoredAsync("Tesla__AccessToken", ct);
        var expiry = await secrets.GetStoredAsync("Tesla__Expires", ct);
        if (access is not null && DateTime.TryParse(expiry, out var until) && until.ToUniversalTime() > DateTime.UtcNow.AddMinutes(2)) { await tx.CommitAsync(ct); return access; }
        var refresh = await secrets.GetRefreshTokenAsync(ct) ?? throw new InvalidOperationException("Tesla refresh token is not configured. Authorize your app with energy_device_data and offline_access.");
        var clientId = await secrets.GetAsync("Tesla__ClientId", ct) ?? throw new InvalidOperationException("Tesla client ID is not configured.");
        using var response = await http.PostAsync("https://fleet-auth.prd.vn.cloud.tesla.com/oauth2/v3/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["refresh_token"] = refresh }), ct);
        response.EnsureSuccessStatusCode(); var token = await response.Content.ReadFromJsonAsync<TeslaToken>(ct) ?? throw new InvalidOperationException("Empty Tesla token response.");
        await secrets.PutAsync("Tesla__RefreshToken", token.RefreshToken, ct); await secrets.PutAsync("Tesla__AccessToken", token.AccessToken, ct); await secrets.PutAsync("Tesla__Expires", DateTime.UtcNow.AddSeconds(token.ExpiresIn).ToString("O"), ct);
        await tx.CommitAsync(ct); return token.AccessToken;
    }
    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        var token = await TokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct); return json.GetProperty("response").Clone();
    }
    public async Task<IReadOnlyList<TeslaSite>> SitesAsync(CancellationToken ct)
    {
        var response = await GetAsync("/api/1/products", ct);
        return response.EnumerateArray().Where(r => r.TryGetProperty("energy_site_id", out var id) && id.ValueKind == JsonValueKind.Number).Select(r => new TeslaSite(r.GetProperty("energy_site_id").GetInt64(), r.TryGetProperty("site_name", out var name) ? name.GetString() : "Powerwall")).ToList();
    }
    public async Task<BatteryLive> LiveAsync(string siteId, CancellationToken ct)
    {
        var response = await GetAsync($"/api/1/energy_sites/{Uri.EscapeDataString(siteId)}/live_status", ct);
        var live = response.Deserialize<TeslaLive>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var info = await GetAsync($"/api/1/energy_sites/{Uri.EscapeDataString(siteId)}/site_info", ct);
        var reserve = info.TryGetProperty("backup_reserve_percent", out var value) ? value.GetDecimal() : info.TryGetProperty("user_settings", out var user) && user.TryGetProperty("backup_reserve_percent", out value) ? value.GetDecimal() : 0;
        return new(live, reserve, DateTime.UtcNow);
    }
    public async Task<IReadOnlyList<BatteryInterval>> HistoryAsync(string siteId, DateTime start, DateTime end, CancellationToken ct)
    {
        var response = await GetAsync($"/api/1/energy_sites/{Uri.EscapeDataString(siteId)}/calendar_history?kind=energy&period=day&start_date={Uri.EscapeDataString(start.ToString("O"))}&end_date={Uri.EscapeDataString(end.ToString("O"))}&time_zone=Europe%2FLondon", ct);
        return NormalizeHistory(response);
    }
    public static IReadOnlyList<BatteryInterval> NormalizeHistory(JsonElement response)
    {
        var series = response.GetProperty("time_series").EnumerateArray().OrderBy(r => r.GetProperty("timestamp").GetDateTimeOffset()).ToArray();
        if (series.Length < 2) throw new InvalidOperationException("Tesla history interval cannot be inferred from fewer than two samples.");
        var result = new Dictionary<DateTime, BatteryInterval>();
        for (var i = 0; i < series.Length; i++)
        {
            var row = series[i]; var start = row.GetProperty("timestamp").GetDateTimeOffset().UtcDateTime;
            var duration = i + 1 < series.Length ? series[i + 1].GetProperty("timestamp").GetDateTimeOffset().UtcDateTime - start : start - series[i - 1].GetProperty("timestamp").GetDateTimeOffset().UtcDateTime;
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(30)) throw new InvalidOperationException("Tesla returned coarse or gapped history. Half-hour allocation is unavailable; no invented battery slots were saved.");
            decimal Energy(string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() / 1000m : 0m;
            var cursor = start; var end = start + duration;
            while (cursor < end)
            {
                var slot = EnergyTime.SlotStart(cursor); var stop = slot.AddMinutes(30) < end ? slot.AddMinutes(30) : end;
                var fraction = (decimal)(stop - cursor).Ticks / duration.Ticks;
                if (!result.TryGetValue(slot, out var b)) { b = new BatteryInterval { IntervalStartUtc = slot }; result.Add(slot, b); }
                b.GridImportKwh += Energy("grid_energy_imported") * fraction;
                b.GridExportKwh += (Energy("grid_energy_exported_from_solar") + Energy("grid_energy_exported_from_battery")) * fraction;
                b.BattChargeKwh += (Energy("battery_energy_imported_from_grid") + Energy("battery_energy_imported_from_solar")) * fraction;
                b.BattDischargeKwh += (Energy("battery_energy_exported") != 0 ? Energy("battery_energy_exported") : Energy("consumer_energy_imported_from_battery")) * fraction;
                b.HomeKwh += (Energy("consumer_energy_imported_from_grid") + Energy("consumer_energy_imported_from_solar") + Energy("consumer_energy_imported_from_battery")) * fraction;
                b.SolarKwh += Energy("solar_energy_exported") * fraction;
                if (row.TryGetProperty("percentage_charged", out var soc)) b.SocEndPct = soc.GetDecimal();
                cursor = stop;
            }
        }
        return result.Values.ToList();
    }
}

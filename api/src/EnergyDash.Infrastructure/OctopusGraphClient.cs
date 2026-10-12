using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnergyDash.Core;

namespace EnergyDash.Infrastructure;

public sealed record FlexDispatch(DateTime Start, DateTime End, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? EnergyAddedKwh, string? Type);
public sealed class OctopusGraphClient(HttpClient http, SecretStore secrets, SettingsStore settings)
{
    public async Task<JsonElement> QueryAsync(string query, object variables, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, http.BaseAddress) { Content = JsonContent.Create(new { query, variables }) };
        if (token is not null) request.Headers.TryAddWithoutValidation("Authorization", token);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        // Supplier error payloads may echo credentials. Use a bounded non-secret error.
        if (json.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0) throw new InvalidOperationException("Octopus GraphQL rejected the query; check account permissions and live schema.");
        return json.GetProperty("data").Clone();
    }
    public async Task<string> TokenAsync(CancellationToken ct)
    {
        var expiry = await secrets.GetStoredAsync("Octopus__TokenExpires", ct);
        var cached = await secrets.GetStoredAsync("Octopus__Token", ct);
        if (cached is not null && DateTime.TryParse(expiry, out var until) && until.ToUniversalTime() > DateTime.UtcNow.AddMinutes(2)) return cached;
        var apiKey = await secrets.GetAsync("Octopus__ApiKey", ct) ?? throw new InvalidOperationException("Octopus API key is not configured.");
        var data = await QueryAsync("mutation Token($key: String!) { obtainKrakenToken(input: {APIKey: $key}) { token } }", new { key = apiKey }, null, ct);
        var token = data.GetProperty("obtainKrakenToken").GetProperty("token").GetString()!;
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/'); payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        using var decoded = JsonDocument.Parse(Convert.FromBase64String(payload));
        var expires = DateTimeOffset.FromUnixTimeSeconds(decoded.RootElement.GetProperty("exp").GetInt64()).UtcDateTime;
        await secrets.PutAsync("Octopus__Token", token, ct); await secrets.PutAsync("Octopus__TokenExpires", expires.ToString("O"), ct); return token;
    }
    public async Task<IReadOnlyList<Dispatch>> DispatchesAsync(CancellationToken ct)
    {
        var token = await TokenAsync(ct);
        var root = await QueryAsync("query Root { __schema { queryType { name } } }", new { }, token, ct);
        var rootName = root.GetProperty("__schema").GetProperty("queryType").GetProperty("name").GetString();
        var schema = await QueryAsync("query Schema($name: String!) { __type(name: $name) { fields(includeDeprecated: true) { name args { name } } } }", new { name = rootName }, token, ct);
        var fields = schema.GetProperty("__type").GetProperty("fields").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToHashSet();
        var config = await settings.GetAsync(ct); var now = DateTime.UtcNow;
        if (fields.Contains("flexPlannedDispatches") && !string.IsNullOrWhiteSpace(config.SmartFlexDeviceId))
        {
            var data = await QueryAsync("query Dispatches($id: String!) { flexPlannedDispatches(deviceId: $id) { start end energyAddedKwh type } }", new { id = config.SmartFlexDeviceId }, token, ct);
            var values = data.GetProperty("flexPlannedDispatches").Deserialize<List<FlexDispatch>>(ProviderJson.Options) ?? [];
            return values.Select(d => new Dispatch { StartUtc = d.Start, EndUtc = d.End, DeltaKwh = d.EnergyAddedKwh, DeviceId = config.SmartFlexDeviceId, Source = "Flex", IsBoost = d.Type is not null && d.Type != "SMART", FirstSeenUtc = now, LastSeenUtc = now }).ToList();
        }
        var account = await secrets.GetAsync("Octopus__AccountNumber", ct);
        var results = new List<Dispatch>();
        foreach (var field in new[] { "plannedDispatches", "completedDispatches" }.Where(fields.Contains))
        {
            var data = await QueryAsync($"query Dispatches($account: String!) {{ {field}(accountNumber: $account) {{ start end delta }} }}", new { account }, token, ct);
            foreach (var row in data.GetProperty(field).EnumerateArray())
                results.Add(new Dispatch { StartUtc = row.GetProperty("start").GetDateTimeOffset().UtcDateTime, EndUtc = row.GetProperty("end").GetDateTimeOffset().UtcDateTime, DeltaKwh = row.TryGetProperty("delta", out var delta) && delta.ValueKind != JsonValueKind.Null ? decimal.Parse(delta.ToString(), System.Globalization.CultureInfo.InvariantCulture) : null, Source = field == "plannedDispatches" ? "Planned" : "Completed", FirstSeenUtc = now, LastSeenUtc = now });
        }
        if (results.Count == 0 && fields.Contains("flexPlannedDispatches") && string.IsNullOrWhiteSpace(config.SmartFlexDeviceId)) throw new InvalidOperationException("Set SmartFlex device ID in Settings. Current schema requires deviceId; legacy dispatch queries may be unavailable.");
        return results;
    }
}

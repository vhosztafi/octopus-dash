using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EnergyDash.Core;

namespace EnergyDash.Infrastructure;

public sealed record OctopusPage<T>(int Count, string? Next, IReadOnlyList<T> Results);
public sealed record OctopusConsumption(decimal Consumption, [property: JsonPropertyName("interval_start")] DateTime IntervalStart, [property: JsonPropertyName("interval_end")] DateTime IntervalEnd);
public sealed record OctopusRate([property: JsonPropertyName("value_inc_vat")] decimal ValueIncVat, [property: JsonPropertyName("valid_from")] DateTime ValidFrom, [property: JsonPropertyName("valid_to")] DateTime? ValidTo);
public sealed record OctopusAgreement([property: JsonPropertyName("tariff_code")] string TariffCode, [property: JsonPropertyName("valid_from")] DateTime ValidFrom, [property: JsonPropertyName("valid_to")] DateTime? ValidTo);
public sealed record OctopusMeter([property: JsonPropertyName("serial_number")] string SerialNumber);
public sealed record OctopusMeterPoint(string? Mpan, string? Mprn, [property: JsonPropertyName("is_export")] bool IsExport, IReadOnlyList<OctopusMeter> Meters, IReadOnlyList<OctopusAgreement> Agreements);
public sealed record OctopusProperty([property: JsonPropertyName("electricity_meter_points")] IReadOnlyList<OctopusMeterPoint> ElectricityMeterPoints, [property: JsonPropertyName("gas_meter_points")] IReadOnlyList<OctopusMeterPoint> GasMeterPoints);
public sealed record OctopusAccount(IReadOnlyList<OctopusProperty> Properties);

public sealed class OctopusRestClient(HttpClient http, SecretStore secrets)
{
    public static string ProductCode(string tariff)
    {
        var match = Regex.Match(tariff, "^(?:E-[12]R|G-1R)-(.+)-[A-P]$", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidOperationException("Unrecognized tariff code; inspect the Octopus product catalogue.");
        return match.Groups[1].Value;
    }
    private async Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct)
    {
        var uri = new Uri(http.BaseAddress!, path);
        // The pagination URL is supplier data: never forward credentials to another host.
        if (uri.Scheme != "https" || uri.Host != "api.octopus.energy" || !uri.AbsolutePath.StartsWith("/v1/", StringComparison.Ordinal)) throw new InvalidOperationException("Rejected external pagination URL.");
        var key = await secrets.GetAsync("Octopus__ApiKey", ct) ?? throw new InvalidOperationException("Octopus API key is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":")));
        return await http.SendAsync(request, ct);
    }
    public async Task<OctopusAccount> AccountAsync(CancellationToken ct)
    {
        var account = await secrets.GetAsync("Octopus__AccountNumber", ct) ?? throw new InvalidOperationException("Octopus account number is not configured.");
        using var response = await GetAsync($"accounts/{Uri.EscapeDataString(account)}/", ct);
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<OctopusAccount>(ct) ?? throw new InvalidOperationException("Empty account response.");
    }
    public async Task<IReadOnlyList<T>> PagesAsync<T>(string path, CancellationToken ct)
    {
        var result = new List<T>(); var seen = new HashSet<string>(); string? next = path;
        while (next is not null)
        {
            if (!seen.Add(next)) throw new InvalidOperationException("Octopus pagination loop.");
            using var response = await GetAsync(next, ct); response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<OctopusPage<T>>(ct) ?? throw new InvalidOperationException("Empty paginated response.");
            result.AddRange(page.Results); next = page.Next;
        }
        return result;
    }
    private static string Period(DateTime start, DateTime end) => $"period_from={Uri.EscapeDataString(start.ToString("O"))}&period_to={Uri.EscapeDataString(end.ToString("O"))}&page_size=25000&order_by=period";
    public Task<IReadOnlyList<OctopusConsumption>> ConsumptionAsync(Meter meter, DateTime start, DateTime end, CancellationToken ct) => PagesAsync<OctopusConsumption>($"{(meter.Fuel == Fuel.Elec ? "electricity" : "gas")}-meter-points/{Uri.EscapeDataString(meter.MpanMprn)}/meters/{Uri.EscapeDataString(meter.Serial)}/consumption/?{Period(start, end)}", ct);
    public Task<IReadOnlyList<OctopusRate>> RatesAsync(Agreement agreement, string endpoint, DateTime start, DateTime end, CancellationToken ct) => PagesAsync<OctopusRate>($"products/{Uri.EscapeDataString(agreement.ProductCode)}/{(agreement.Fuel == Fuel.Elec ? "electricity" : "gas")}-tariffs/{Uri.EscapeDataString(agreement.TariffCode)}/{endpoint}/?{Period(start, end)}", ct);
    public async Task<IReadOnlyList<(RateRegister Register, OctopusRate Rate)>> UnitRatesAsync(Agreement agreement, DateTime start, DateTime end, CancellationToken ct)
    {
        if (agreement.TariffCode.StartsWith("E-2R-", StringComparison.Ordinal))
        {
            var day = await RatesAsync(agreement, "day-unit-rates", start, end, ct); var night = await RatesAsync(agreement, "night-unit-rates", start, end, ct);
            return day.Select(r => (RateRegister.Day, r)).Concat(night.Select(r => (RateRegister.Night, r))).ToList();
        }
        try { return (await RatesAsync(agreement, "standard-unit-rates", start, end, ct)).Select(r => (RateRegister.Standard, r)).ToList(); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && agreement.TariffCode.StartsWith("E-2R-", StringComparison.Ordinal))
        {
            var day = await RatesAsync(agreement, "day-unit-rates", start, end, ct); var night = await RatesAsync(agreement, "night-unit-rates", start, end, ct);
            return day.Select(r => (RateRegister.Day, r)).Concat(night.Select(r => (RateRegister.Night, r))).ToList();
        }
    }
}

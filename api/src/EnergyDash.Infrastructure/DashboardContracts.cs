using EnergyDash.Core;

namespace EnergyDash.Infrastructure;

public sealed record SlotDto(string StartUtc, string EndUtc, string StartLocal, string EndLocal, string LocalOffset, string Band, decimal? RatePence, string RateSource, decimal? Kwh, decimal? CostPence, decimal? BatteryKwh, decimal? HouseKwh, bool OverLimit);
public sealed record CostRowDto(string Label, string Band, decimal? Kwh, decimal? RatePence, decimal CostPence, bool PerDay);
public sealed record KpisDto(decimal TotalPence, decimal ElectricityPence, decimal GasPence, decimal ImportKwh, decimal? EffectiveRatePence, decimal? OffPeakSharePercent, decimal PeakImportKwh, decimal GasKwh, decimal BatterySavingPence, int AlertCount);
public sealed record PowerwallDto(decimal? ChargePercent, decimal? ReservePercent, decimal? GridWatts, decimal? BatteryWatts, decimal? LoadWatts, decimal? SolarWatts, string? SyncedUtc, decimal ChargedKwh, decimal DischargedKwh, decimal SavingPence);
public sealed record DayDto(string Date, bool Sample, bool Complete, int MissingSlots, int UnpricedSlots, KpisDto Kpis, IReadOnlyList<SlotDto> Slots, IReadOnlyList<CostRowDto> CostByRate, PowerwallDto Powerwall, IReadOnlyList<SlotDto> Alerts, IReadOnlyList<string> Warnings, DashboardSettings Settings);
public sealed record LedgerDto(string Date, bool Sample, string Filter, IReadOnlyList<SlotDto> Slots, decimal ImportKwh, decimal CostPence, Dictionary<string, int> Counts, int MissingSlots);
public sealed record WeekDayDto(string Date, string Name, decimal OffPence, decimal PeakPence, decimal StandingPence, decimal GasPence, decimal TotalPence, bool Complete);
public sealed record WeekAlertDto(string Date, string Time, decimal Kwh, decimal CostPence);
public sealed record WeekDto(string Monday, bool Sample, bool Complete, IReadOnlyList<WeekDayDto> Days, KpisDto Kpis, decimal? PreviousTotalPence, decimal? DifferencePence, int GasOverDays, IReadOnlyList<WeekAlertDto> Alerts, DashboardSettings Settings);
public sealed record StatusDto(bool Sample, string? OctopusDataUtc, string? TeslaSyncUtc, string? PricingUtc, string OctopusHealth, string TeslaHealth, string PricingHealth);
public sealed record LatestDayDto(string? Date);
public sealed record ConnectionDto(string Provider, bool Configured, string? Hint, string? AccountNumber, string Status);
public sealed record ConnectionsDto(ConnectionDto Octopus, ConnectionDto Tesla);
public sealed record ConnectionTestDto(bool Ok, string Message);
public sealed record CommandDto(long Id, string Status);
public sealed record EnergySiteDto(string Id, string Name);
public interface IDashboardData
{
    Task<DayDto> DayAsync(string date, CancellationToken ct);
    Task<WeekDto> WeekAsync(string monday, CancellationToken ct);
    Task<StatusDto> StatusAsync(CancellationToken ct);
    Task<LatestDayDto> LatestAsync(CancellationToken ct);
    Task<PowerwallDto> LiveAsync(CancellationToken ct);
}

public static class Ledger
{
    public static LedgerDto From(DayDto day, string filter)
    {
        bool Keep(SlotDto s, string f) => f == "all" || (f == "over" ? s.OverLimit : s.Band == f);
        var rows = day.Slots.Where(s => Keep(s, filter)).ToArray();
        return new(day.Date, day.Sample, filter, rows, rows.Sum(s => s.Kwh ?? 0), rows.Sum(s => s.CostPence ?? 0), new[] { "all", "peak", "off", "disp", "over" }.ToDictionary(f => f, f => day.Slots.Count(s => Keep(s, f))), rows.Count(s => s.Kwh is null));
    }
    public static string Csv(LedgerDto ledger)
    {
        static string Number(decimal? d) => d?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        static string Cell(string value) => "\"" + (value.Length > 0 && "=+-@".Contains(value[0]) ? "'" : "") + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return "start_utc,end_utc,start_local,offset,band,rate_source,rate_pence,import_kwh,battery_kwh,cost_pence,over_limit\r\n" + string.Join("\r\n", ledger.Slots.Select(s => string.Join(',', Cell(s.StartUtc), Cell(s.EndUtc), Cell(s.StartLocal), Cell(s.LocalOffset), Cell(s.Band), Cell(s.RateSource), Number(s.RatePence), Number(s.Kwh), Number(s.BatteryKwh), Number(s.CostPence), s.OverLimit ? "true" : "false")));
    }
}

using System.Text.Json;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnergyDash.Infrastructure;

public sealed class DashboardQueries(EnergyDbContext db, SettingsStore settings) : IDashboardData
{
    public async Task<DayDto> DayAsync(string date, CancellationToken ct)
    {
        var day = EnergyTime.ParseDay(date); var localDate = EnergyTime.Date(day); var (start, end) = EnergyTime.Bounds(day); var config = await settings.GetAsync(ct);
        var slots = await db.Set<PricedSlot>().AsNoTracking().Where(s => s.SlotStartUtc >= start && s.SlotStartUtc < end).ToListAsync(ct);
        var days = await db.Set<PricedDay>().AsNoTracking().Where(d => d.LocalDate == localDate).ToListAsync(ct);
        var battery = await db.Set<BatteryInterval>().AsNoTracking().Where(b => b.IntervalStartUtc >= start && b.IntervalStartUtc < end).ToListAsync(ct);
        var elec = slots.Where(s => s.Fuel == Fuel.Elec).ToDictionary(s => s.SlotStartUtc); var gas = slots.Where(s => s.Fuel == Fuel.Gas).ToList(); var bats = battery.ToDictionary(b => b.IntervalStartUtc);
        var rows = EnergyTime.Slots(day).Select(time =>
        {
            elec.TryGetValue(time, out var slot); bats.TryGetValue(time, out var b);
            return ToSlot(time, slot, b, config);
        }).ToList();
        var warnings = days.SelectMany(d => JsonSerializer.Deserialize<string[]>(d.WarningsJson) ?? []).Distinct().ToList();
        if (days.Count < 2) warnings.Add("Data has not been priced for both fuels. Sync your account to fill this day.");
        var energy = rows.Sum(s => s.CostPence ?? 0); var import = rows.Sum(s => s.Kwh ?? 0); var off = rows.Where(s => s.Band is "off" or "disp").Sum(s => s.Kwh ?? 0); var peak = rows.Where(s => s.Band == "peak").Sum(s => s.Kwh ?? 0);
        var es = days.FirstOrDefault(d => d.Fuel == Fuel.Elec)?.StandingPence; var gs = days.FirstOrDefault(d => d.Fuel == Fuel.Gas)?.StandingPence;
        var gasCost = gas.Sum(s => s.CostPence ?? 0); var gasKwh = gas.Sum(s => s.Kwh ?? 0);
        var rates = rows.Where(s => s.RatePence.HasValue).Select(s => s.RatePence!.Value).ToArray();
        var low = rates.Length == 0 ? 0 : rates.Min();
        var saving = rows.Where(s => s.Band == "peak" && s.BatteryKwh < 0).Sum(s => -s.BatteryKwh!.Value * Math.Max(0, (s.RatePence ?? low) - low));
        var alerts = config.PeakAlertEnabled ? rows.Where(s => s.OverLimit).ToArray() : [];
        var kpis = new KpisDto(energy + (es ?? 0) + gasCost + (gs ?? 0), energy + (es ?? 0), gasCost + (gs ?? 0), import, import == 0 ? null : energy / import, import == 0 ? null : off / import * 100, peak, gasKwh, saving, alerts.Length);
        var costs = rows.GroupBy(s => new { s.Band, s.RatePence }).Select(g => new CostRowDto(g.Key.Band switch { "off" => "Off-peak window", "disp" => "Dispatch slots", _ => "Peak" }, g.Key.Band, g.Sum(s => s.Kwh ?? 0), g.Key.RatePence, g.Sum(s => s.CostPence ?? 0), false)).ToList();
        costs.Add(new("Elec. standing", "standing", null, es, es ?? 0, true));
        costs.AddRange(gas.GroupBy(s => s.RatePence).Select(g => new CostRowDto("Gas", "gas", g.Sum(s => s.Kwh ?? 0), g.Key, g.Sum(s => s.CostPence ?? 0), false)));
        costs.Add(new("Gas standing", "standing", null, gs, gs ?? 0, true));
        var live = await LiveAsync(ct);
        var missing = rows.Count(s => s.Kwh is null) + (days.FirstOrDefault(d => d.Fuel == Fuel.Gas)?.MissingSlots ?? rows.Count);
        var unpriced = rows.Count(s => s.RatePence is null) + (days.FirstOrDefault(d => d.Fuel == Fuel.Gas)?.UnpricedSlots ?? rows.Count);
        var complete = missing == 0 && unpriced == 0 && es is not null && gs is not null;
        if (config.PriceDispatchAtOffPeak && elec.Values.Any(s => s.TariffCode.Contains("INTELLI-", StringComparison.Ordinal) || s.TariffCode.Contains("IOG-", StringComparison.Ordinal)))
        {
            var dispatch = await db.Set<SyncLog>().AsNoTracking().Where(l => l.Source == "Dispatch").OrderByDescending(l => l.StartedUtc).ThenByDescending(l => l.Id).FirstOrDefaultAsync(ct);
            if (dispatch is null || !dispatch.Ok || dispatch.FinishedUtc is null)
            {
                complete = false;
                warnings.Add("Smart-charge dispatches have not synced. Costs use tariff rates; extra discounted charging slots may be missing. Check the SmartFlex device ID in Settings.");
            }
        }
        return new(date, false, complete, missing, unpriced, kpis, rows, costs, live with { ChargedKwh = battery.Where(b => elec.TryGetValue(b.IntervalStartUtc, out var priced) && priced.Band is RateBand.Off or RateBand.Disp).Sum(b => b.BattChargeKwh), DischargedKwh = rows.Where(s => s.Band == "peak" && s.BatteryKwh < 0).Sum(s => -s.BatteryKwh!.Value), SavingPence = saving }, alerts, warnings, config);
    }
    public static SlotDto ToSlot(DateTime time, PricedSlot? slot, BatteryInterval? battery, DashboardSettings config)
    {
        var local = EnergyTime.Instant(time).InZone(EnergyTime.London); var end = EnergyTime.Instant(time.AddMinutes(30)).InZone(EnergyTime.London);
        return new(time.ToString("O"), time.AddMinutes(30).ToString("O"), $"{local.Hour:00}:{local.Minute:00}", $"{end.Hour:00}:{end.Minute:00}", local.Offset.ToString(), slot?.Band.ToString().ToLowerInvariant() ?? "peak", slot?.RatePence, slot?.RateSource ?? "Not yet priced", slot?.Kwh, slot?.CostPence, battery is null ? null : battery.BattChargeKwh - battery.BattDischargeKwh, battery?.HomeKwh, slot?.Kwh is { } k && AlertMath.PeakEligible(slot.Band, config) && k > config.PeakLimitKwh);
    }
    public async Task<WeekDto> WeekAsync(string monday, CancellationToken ct)
    {
        var first = EnergyTime.ParseDay(monday); var days = new List<DayDto>(); var previous = new List<DayDto>();
        for (var i = 0; i < 7; i++) { days.Add(await DayAsync(first.PlusDays(i).ToString("yyyy-MM-dd", null), ct)); previous.Add(await DayAsync(first.PlusDays(i - 7).ToString("yyyy-MM-dd", null), ct)); }
        return BuildWeek(monday, days, previous);
    }
    public static WeekDto BuildWeek(string monday, IReadOnlyList<DayDto> days, IReadOnlyList<DayDto> previous)
    {
        var config = days[0].Settings; var total = days.Sum(d => d.Kpis.TotalPence); var energy = days.Sum(d => d.Slots.Sum(s => s.CostPence ?? 0)); var kwh = days.Sum(d => d.Kpis.ImportKwh); var off = days.SelectMany(d => d.Slots).Where(s => s.Band is "off" or "disp").Sum(s => s.Kwh ?? 0);
        var kpis = new KpisDto(total, days.Sum(d => d.Kpis.ElectricityPence), days.Sum(d => d.Kpis.GasPence), kwh, kwh == 0 ? null : energy / kwh, kwh == 0 ? null : off / kwh * 100, days.Sum(d => d.Kpis.PeakImportKwh), days.Sum(d => d.Kpis.GasKwh), days.Sum(d => d.Kpis.BatterySavingPence), days.Sum(d => d.Alerts.Count));
        var bars = days.Select(d => new WeekDayDto(d.Date, EnergyTime.ParseDay(d.Date).DayOfWeek.ToString()[..3], d.CostByRate.Where(c => c.Band is "off" or "disp").Sum(c => c.CostPence), d.CostByRate.Where(c => c.Band == "peak").Sum(c => c.CostPence), d.CostByRate.Where(c => c.Label == "Elec. standing").Sum(c => c.CostPence), d.Kpis.GasPence, d.Kpis.TotalPence, d.Complete)).ToList();
        decimal? prior = previous.Count == 7 && previous.All(d => d.Complete) ? previous.Sum(d => d.Kpis.TotalPence) : null;
        return new(monday, days[0].Sample, days.All(d => d.Complete), bars, kpis, prior, days.All(d => d.Complete) && prior is not null ? total - prior : null, days.Count(d => d.Complete && d.Kpis.GasPence > config.GasDailyLimitPounds * 100), days.SelectMany(d => d.Alerts.Select(s => new WeekAlertDto(d.Date, s.StartLocal + "–" + s.EndLocal + " " + s.LocalOffset, s.Kwh ?? 0, s.CostPence ?? 0))).ToList(), config);
    }
    public async Task<StatusDto> StatusAsync(CancellationToken ct)
    {
        string[] sources = ["OctopusAccount", "OctopusRates", "OctopusConsumption", "TeslaLive", "Pricing", "Reprice", "Dispatch"];
        var logs = await db.Set<SyncLog>().AsNoTracking().Where(s => sources.Contains(s.Source)).GroupBy(s => s.Source).Select(g => g.OrderByDescending(s => s.StartedUtc).ThenByDescending(s => s.Id).First()).ToListAsync(ct);
        string Health(string source) { var last = logs.FirstOrDefault(l => l.Source == source); return last is null ? "Not connected" : last.FinishedUtc is null ? "Syncing" : last.Ok ? "Connected" : "Sync failed"; }
        var octopusHealth = new[] { "OctopusAccount", "OctopusRates", "OctopusConsumption" }.Select(Health).ToArray();
        var pricingLog = logs.Where(l => l.Source is "Pricing" or "Reprice" or "OctopusRates" or "OctopusConsumption" or "Dispatch" && l.Ok).OrderByDescending(l => l.FinishedUtc).FirstOrDefault();
        var consumption = await db.Set<Consumption>().MaxAsync(c => (DateTime?)c.IntervalEndUtc, ct); var pricing = await db.Set<PricedDay>().MaxAsync(d => (DateTime?)d.PricedUtc, ct);
        return new(false, consumption?.ToString("O"), logs.FirstOrDefault(l => l.Source == "TeslaLive" && l.Ok)?.FinishedUtc?.ToString("O"), pricing?.ToString("O"), octopusHealth.Contains("Syncing") ? "Syncing" : octopusHealth.Contains("Sync failed") ? "Sync failed" : octopusHealth.All(h => h == "Connected") ? "Connected" : "Not connected", Health("TeslaLive"), pricingLog is null ? "Not connected" : "Connected");
    }
    public async Task<LatestDayDto> LatestAsync(CancellationToken ct)
    {
        var yesterday = EnergyTime.Date(EnergyTime.LocalDay(DateTime.UtcNow).PlusDays(-1));
        var days = await db.Set<PricedDay>().AsNoTracking().Where(d => d.LocalDate <= yesterday && d.MissingSlots == 0 && d.UnpricedSlots == 0 && d.StandingPence != null).ToListAsync(ct);
        var latest = days.GroupBy(d => d.LocalDate).Where(g => g.Select(d => d.Fuel).Distinct().Count() == 2).OrderByDescending(g => g.Key).FirstOrDefault();
        return new(latest?.Key.ToString("yyyy-MM-dd"));
    }
    public async Task<PowerwallDto> LiveAsync(CancellationToken ct)
    {
        var value = await db.Set<Setting>().AsNoTracking().SingleOrDefaultAsync(s => s.Key == "batteryLive", ct);
        var live = value is null ? null : JsonSerializer.Deserialize<BatteryLive>(value.ValueJson);
        return new(live?.Status.PercentageCharged, live?.Reserve, live?.Status.GridPower, live?.Status.BatteryPower, live?.Status.LoadPower, live?.Status.SolarPower, live?.SyncedUtc.ToString("O"), 0, 0, 0);
    }
}

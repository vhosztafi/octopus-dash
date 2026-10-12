using EnergyDash.Core;

namespace EnergyDash.Infrastructure;

// Only enabled explicitly. All prices below are the supplied prototype's SAMPLE figures.
public sealed class SampleDashboard : IDashboardData
{
    public DashboardSettings Settings { get; set; } = new() { WeeklyEnabled = true };
    public Task<DayDto> DayAsync(string date, CancellationToken ct)
    {
        var day = EnergyTime.ParseDay(date); var starts = EnergyTime.Slots(day); var rows = new List<SlotDto>();
        for (var i = 0; i < starts.Count; i++)
        {
            var local = EnergyTime.Instant(starts[i]).InZone(EnergyTime.London); var index = local.Hour * 2 + local.Minute / 30;
            var off = index >= 47 || index < 11; var dispatch = index is >= 26 and <= 28;
            var band = off ? RateBand.Off : dispatch && Settings.PriceDispatchAtOffPeak ? RateBand.Disp : RateBand.Peak;
            var house = index < 12 ? .25m : index < 18 ? .45m : index < 34 ? .30m : index < 44 ? .65m : .35m;
            decimal import, batt;
            if (!off && !dispatch) { import = index switch { 15 => .12m, 36 => .42m, 37 => .35m, 40 => .18m, _ => .02m + index * 7 % 5 * .01m }; batt = -Math.Max(0, house - import); }
            else { var ev = index <= 7 || dispatch ? 3.5m : 0; var charge = index is >= 1 and <= 8 ? 1.5m : dispatch ? .8m : 0; import = house + ev + charge; batt = charge; }
            var rate = band == RateBand.Peak ? 27.5m : 7.5m;
            rows.Add(DashboardQueries.ToSlot(starts[i], new PricedSlot { Band = band, Kwh = import, CostPence = import * rate, RatePence = rate, RateSource = band == RateBand.Off ? "Tariff window 23:30–05:30" : band == RateBand.Disp ? "Smart-charge dispatch" : "Tariff schedule" }, new BatteryInterval { BattChargeKwh = Math.Max(0, batt), BattDischargeKwh = Math.Max(0, -batt), HomeKwh = house }, Settings));
        }
        var kwh = rows.Sum(s => s.Kwh ?? 0); var energy = rows.Sum(s => s.CostPence ?? 0); var gas = 38.4m * 6.12m + 31.4m;
        var offKwh = rows.Where(s => s.Band is "off" or "disp").Sum(s => s.Kwh ?? 0); var peak = rows.Where(s => s.Band == "peak").Sum(s => s.Kwh ?? 0);
        var battOut = rows.Where(s => s.Band == "peak" && s.BatteryKwh < 0).Sum(s => -s.BatteryKwh!.Value); var saving = battOut * 20m;
        var costs = rows.GroupBy(s => s.Band).Select(g => new CostRowDto(g.Key == "off" ? "Off-peak window" : g.Key == "disp" ? "Dispatch slots" : "Peak", g.Key, g.Sum(s => s.Kwh ?? 0), g.First().RatePence, g.Sum(s => s.CostPence ?? 0), false)).ToList();
        costs.AddRange([new("Elec. standing", "standing", null, 53.6m, 53.6m, true), new("Gas", "gas", 38.4m, 6.12m, 38.4m * 6.12m, false), new("Gas standing", "standing", null, 31.4m, 31.4m, true)]);
        var alerts = Settings.PeakAlertEnabled ? rows.Where(s => s.OverLimit).ToArray() : [];
        return Task.FromResult(new DayDto(date, true, true, 0, 0, new(energy + 53.6m + gas, energy + 53.6m, gas, kwh, energy / kwh, offKwh / kwh * 100, peak, 38.4m, saving, alerts.Length), rows, costs, new(78, 20, 40, 600, 650, 0, "2026-10-12T05:38:00Z", rows.Sum(s => Math.Max(0, s.BatteryKwh ?? 0)), battOut, saving), alerts, [], Settings));
    }
    public async Task<WeekDto> WeekAsync(string monday, CancellationToken ct)
    {
        var first = EnergyTime.ParseDay(monday); var days = new List<DayDto>();
        for (var i = 0; i < 7; i++) days.Add(await DayAsync(first.PlusDays(i).ToString("yyyy-MM-dd", null), ct));
        var week = DashboardQueries.BuildWeek(monday, days, days);
        var source = new[] { (1.95m, .42m, 2.40m), (2.60m, .31m, 2.55m), (1.20m, .88m, 2.70m), (2.85m, .27m, 3.35m), (1.70m, .36m, 2.90m), (3.10m, .52m, 3.12m), (4.27m, .61m, 2.66m) };
        var bars = source.Select((d, i) => new WeekDayDto(days[i].Date, week.Days[i].Name, d.Item1 * 100, d.Item2 * 100, 53.6m, d.Item3 * 100, (d.Item1 + d.Item2 + d.Item3) * 100 + 53.6m, true)).ToArray();
        var total = bars.Sum(d => d.TotalPence);
        var eventSource = new[] { (0, "18:00–18:30", .34m), (2, "07:30–08:00", .31m), (2, "17:30–18:00", .52m), (2, "18:00–18:30", .47m), (4, "19:00–19:30", .33m), (5, "12:00–12:30", .38m), (5, "18:30–19:00", .36m), (6, "18:00–18:30", .42m), (6, "18:30–19:00", .35m) };
        var events = Settings.PeakAlertEnabled ? eventSource.Where(e => e.Item3 > Settings.PeakLimitKwh).Select(e => new WeekAlertDto(days[e.Item1].Date, e.Item2, e.Item3, e.Item3 * 27.5m)).ToArray() : [];
        var importedOff = bars.Sum(d => d.OffPence) / 7.5m; var importedPeak = bars.Sum(d => d.PeakPence) / 27.5m;
        return week with { Days = bars, PreviousTotalPence = 3480, DifferencePence = total - 3480, Alerts = events, GasOverDays = bars.Count(d => d.GasPence > Settings.GasDailyLimitPounds * 100), Kpis = week.Kpis with { TotalPence = total, GasPence = bars.Sum(d => d.GasPence), ElectricityPence = bars.Sum(d => d.OffPence + d.PeakPence + d.StandingPence), ImportKwh = importedOff + importedPeak, OffPeakSharePercent = importedOff / (importedOff + importedPeak) * 100, AlertCount = events.Length, BatterySavingPence = 1640 } };
    }
    public Task<LatestDayDto> LatestAsync(CancellationToken ct) => Task.FromResult(new LatestDayDto("2026-10-11"));
    public Task<StatusDto> StatusAsync(CancellationToken ct) => Task.FromResult(new StatusDto(true, "2026-10-11T23:00:00Z", "2026-10-12T05:38:00Z", "2026-10-12T05:38:00Z", "Sample figures", "Sample figures", "Sample figures"));
    public async Task<PowerwallDto> LiveAsync(CancellationToken ct) => (await DayAsync("2026-10-11", ct)).Powerwall;
}

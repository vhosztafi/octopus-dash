namespace EnergyDash.Core;

public sealed record DashboardSettings
{
    public bool PriceDispatchAtOffPeak { get; init; } = true;
    public bool DispatchAllowanceEnabled { get; init; }
    public int DispatchAllowanceSlots { get; init; } = 12;
    public bool CountScheduledWindowDispatches { get; init; } = true;
    public string NightStart { get; init; } = "23:30";
    public string NightEnd { get; init; } = "05:30";
    public string GasUnit { get; init; } = "m3";
    public decimal CalorificValue { get; init; } = 39.5m;
    public decimal PeakLimitKwh { get; init; } = .30m;
    public bool PeakAlertEnabled { get; init; } = true;
    public string PeakSource { get; init; } = "tesla";
    public bool IgnoreDispatch { get; init; } = true;
    public bool EarlyWarning { get; init; } = true;
    public bool RenotifyReconciliation { get; init; }
    public decimal GasDailyLimitPounds { get; init; } = 3m;
    public bool GasAlertEnabled { get; init; } = true;
    public bool NotifyPush { get; init; } = true;
    public bool NotifyEmail { get; init; }
    public int CooldownMinutes { get; init; } = 60;
    public bool WeeklyEnabled { get; init; }
    public string WeekDay { get; init; } = "mon";
    public string WeekTime { get; init; } = "08:00";
    public string Recipients { get; init; } = "";
    public bool IncludeCosts { get; init; } = true;
    public bool IncludeAlerts { get; init; } = true;
    public bool IncludeGas { get; init; } = true;
    public bool IncludeBattery { get; init; } = true;
    public bool AttachCsv { get; init; }
    public int HistoryDays { get; init; } = 90;
    public string? TeslaSiteId { get; init; }
    public string? SmartFlexDeviceId { get; init; }
    public PricingSettings Pricing => new(PriceDispatchAtOffPeak, DispatchAllowanceEnabled, DispatchAllowanceSlots, CountScheduledWindowDispatches, NightStart, NightEnd);
}

public static class AlertMath
{
    public static bool PeakEligible(RateBand band, DashboardSettings settings) => band == RateBand.Peak || (!settings.IgnoreDispatch && band == RateBand.Disp);
    public static bool Exceeds(decimal value, decimal limit) => value > limit;
}

public sealed record PowerSample(DateTime AtUtc, decimal GridWatts);
public sealed record ImportContribution(DateTime SlotStartUtc, decimal Wh);
public static class LiveIntegration
{
    public static IReadOnlyList<ImportContribution> Between(PowerSample previous, PowerSample current)
    {
        // Gaps > 2 min cannot be estimated safely. Export never cancels import.
        if (current.AtUtc <= previous.AtUtc || current.AtUtc - previous.AtUtc > TimeSpan.FromMinutes(2)) return [];
        var result = new List<ImportContribution>();
        var cursor = previous.AtUtc;
        while (cursor < current.AtUtc)
        {
            var start = EnergyTime.SlotStart(cursor);
            var end = current.AtUtc < start.AddMinutes(30) ? current.AtUtc : start.AddMinutes(30);
            var span = (decimal)(current.AtUtc - previous.AtUtc).Ticks;
            decimal PowerAt(DateTime t) => previous.GridWatts + (current.GridWatts - previous.GridWatts) * (decimal)(t - previous.AtUtc).Ticks / span;
            var a = PowerAt(cursor); var b = PowerAt(end);
            var ticks = (decimal)(end - cursor).Ticks;
            var wh = a >= 0 && b >= 0 ? (a + b) * ticks / (2m * TimeSpan.TicksPerHour) : a <= 0 && b <= 0 ? 0 : ticks * (a > 0 ? a * a : b * b) / (2m * Math.Abs(b - a) * TimeSpan.TicksPerHour);
            result.Add(new(start, wh)); cursor = end;
        }
        return result;
    }
}

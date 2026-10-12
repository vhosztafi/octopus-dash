using NodaTime;

namespace EnergyDash.Core;

public sealed record ConsumptionInterval(DateTime StartUtc, DateTime EndUtc, decimal Kwh);
public sealed record DispatchWindow(DateTime StartUtc, DateTime EndUtc, string DeviceId = "", bool IsBoost = false);
public sealed record PricingSettings(bool PriceDispatchAtOffPeak = true, bool DispatchAllowanceEnabled = false, int DispatchAllowanceSlots = 12, bool CountScheduledWindowDispatches = true, string NightStart = "23:30", string NightEnd = "05:30");
public sealed record PricingInput(LocalDate Day, Fuel Fuel, IReadOnlyList<ConsumptionInterval> Consumption, IReadOnlyList<Agreement> Agreements, IReadOnlyList<UnitRate> Rates, IReadOnlyList<StandingCharge> StandingCharges, IReadOnlyList<DispatchWindow> Dispatches, PricingSettings Settings);
public sealed record PricedSlotResult(DateTime StartUtc, DateTime EndUtc, decimal? Kwh, decimal? RatePence, RateBand Band, string RateSource, decimal? CostPence, string TariffCode);
public sealed record PricedDayResult(LocalDate Day, IReadOnlyList<PricedSlotResult> Slots, decimal? StandingPence, IReadOnlyList<string> Warnings)
{
    public decimal EnergyCostPence => Slots.Sum(s => s.CostPence ?? 0);
    public int MissingSlots => Slots.Count(s => s.Kwh is null);
    public int UnpricedSlots => Slots.Count(s => s.RatePence is null);
}

public static class GasConversion
{
    public static decimal ToKwh(decimal value, string unit, decimal calorificValue) => unit == "m3" ? value * 1.02264m * calorificValue / 3.6m : value;
}

public sealed class DispatchAllowancePolicy
{
    // TODO(verify): planned snapshots do not establish active charging. Confirm account-specific
    // rollout, device identity, scheduled-window counting and Boost treatment before enabling.
    public bool Granted(PricingInput input, DateTime slot, DispatchWindow dispatch, Func<DateTime, bool> isOff)
    {
        if (!input.Settings.DispatchAllowanceEnabled) return true;
        var noonDay = EnergyTime.AllowanceDay(slot);
        var noon = EnergyTime.Utc(noonDay.At(new LocalTime(12, 0)).InZoneStrictly(EnergyTime.London).ToInstant());
        var end = EnergyTime.Utc(noonDay.PlusDays(1).At(new LocalTime(12, 0)).InZoneStrictly(EnergyTime.London).ToInstant());
        var count = 0;
        for (var candidate = noon; candidate < end && candidate <= slot; candidate = candidate.AddMinutes(30))
            if (input.Dispatches.Any(d => !d.IsBoost && d.DeviceId == dispatch.DeviceId && d.StartUtc < candidate.AddMinutes(30) && d.EndUtc > candidate)
                && (input.Settings.CountScheduledWindowDispatches || !isOff(candidate))) count++;
        return count <= input.Settings.DispatchAllowanceSlots;
    }
}

public sealed class PricingEngine
{
    public PricedDayResult PriceDay(PricingInput input)
    {
        var warnings = new HashSet<string>();
        var slots = new List<PricedSlotResult>();
        var allowance = new DispatchAllowancePolicy();
        var starts = EnergyTime.Slots(input.Day);
        UnitRate? RateAt(DateTime time)
        {
            var agreement = input.Agreements.Where(a => a.Fuel == input.Fuel && EnergyTime.Contains(a.ValidFromUtc, a.ValidToUtc, time)).OrderByDescending(a => a.ValidFromUtc).FirstOrDefault();
            if (agreement is null) return null;
            var local = EnergyTime.Instant(time).InZone(EnergyTime.London).TimeOfDay;
            var nightStart = NodaTime.Text.LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(input.Settings.NightStart).GetValueOrThrow();
            var nightEnd = NodaTime.Text.LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(input.Settings.NightEnd).GetValueOrThrow();
            var night = nightStart > nightEnd ? local >= nightStart || local < nightEnd : local >= nightStart && local < nightEnd;
            var register = night ? RateRegister.Night : RateRegister.Day;
            return input.Rates.Where(r => r.Fuel == input.Fuel && r.TariffCode == agreement.TariffCode && EnergyTime.Contains(r.ValidFromUtc, r.ValidToUtc, time) && (r.Register == RateRegister.Standard || r.Register == register)).OrderByDescending(r => r.ValidFromUtc).FirstOrDefault();
        }
        decimal? Lowest(DateTime time)
        {
            var daySlots = EnergyTime.Slots(EnergyTime.LocalDay(time));
            var tariff = RateAt(time)?.TariffCode;
            return daySlots.Select(RateAt).Where(r => r is not null && r.TariffCode == tariff).Select(r => (decimal?)r!.PenceIncVat).Min();
        }
        bool IsOff(DateTime time) => RateAt(time) is { } rate && rate.PenceIncVat == Lowest(time);
        foreach (var start in starts)
        {
            var rate = RateAt(start);
            var agreement = input.Agreements.Where(a => a.Fuel == input.Fuel && EnergyTime.Contains(a.ValidFromUtc, a.ValidToUtc, start)).OrderByDescending(a => a.ValidFromUtc).FirstOrDefault();
            var matches = input.Consumption.Where(c => c.StartUtc == start && c.EndUtc == start.AddMinutes(30)).ToList();
            decimal? kwh = matches.Count == 1 ? matches[0].Kwh : null;
            decimal? pence = rate?.PenceIncVat;
            var band = input.Fuel == Fuel.Gas ? RateBand.Std : IsOff(start) ? RateBand.Off : RateBand.Peak;
            var source = rate is null ? "Missing agreement or unit rate" : band == RateBand.Off ? "Tariff off-peak window" : "Tariff schedule";
            var overlapping = input.Dispatches.Where(d => !d.IsBoost && d.StartUtc < start.AddMinutes(30) && d.EndUtc > start).ToList();
            if (input.Fuel == Fuel.Elec && band == RateBand.Peak && input.Settings.PriceDispatchAtOffPeak && overlapping.Count > 0 && pence is not null)
            {
                if (overlapping.Any(d => allowance.Granted(input, start, d, IsOff))) { pence = Lowest(start); band = RateBand.Disp; source = "Smart-charge dispatch"; }
                else source = "Dispatch beyond daily allowance";
            }
            if (kwh is null) warnings.Add("Consumption is missing or not a unique half-hour reading; totals are partial.");
            if (pence is null) warnings.Add("Agreement or VAT-inclusive rate is missing; unpriced energy is excluded from cost totals.");
            slots.Add(new(start, start.AddMinutes(30), kwh, pence, band, source, kwh * pence, agreement?.TariffCode ?? ""));
        }
        var midnight = starts[0];
        var tariffAtMidnight = input.Agreements.Where(a => a.Fuel == input.Fuel && EnergyTime.Contains(a.ValidFromUtc, a.ValidToUtc, midnight)).OrderByDescending(a => a.ValidFromUtc).FirstOrDefault()?.TariffCode;
        var standing = input.StandingCharges.Where(s => s.Fuel == input.Fuel && s.TariffCode == tariffAtMidnight && EnergyTime.Contains(s.ValidFromUtc, s.ValidToUtc, midnight)).OrderByDescending(s => s.ValidFromUtc).FirstOrDefault()?.PencePerDayIncVat;
        if (standing is null) warnings.Add("Standing charge is missing; day total is incomplete.");
        return new(input.Day, slots, standing, warnings.ToArray());
    }
}

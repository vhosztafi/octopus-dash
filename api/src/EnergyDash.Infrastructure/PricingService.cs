using System.Text.Json;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnergyDash.Infrastructure;

public sealed class PricingService(EnergyDbContext db, SettingsStore settings)
{
    public async Task<PricedDayResult> ComputeAsync(LocalDate day, Fuel fuel, CancellationToken ct)
    {
        var (start, end) = EnergyTime.Bounds(day); var extendedStart = EnergyTime.Bounds(day.PlusDays(-1)).Start; var extendedEnd = EnergyTime.Bounds(day.PlusDays(1)).End;
        var config = await settings.GetAsync(ct);
        var readings = await db.Set<Consumption>().AsNoTracking().Where(c => c.Fuel == fuel && c.IntervalStartUtc >= start && c.IntervalStartUtc < end).ToListAsync(ct);
        var consumption = readings.Select(c => new ConsumptionInterval(c.IntervalStartUtc, c.IntervalEndUtc, fuel == Fuel.Gas ? GasConversion.ToKwh(c.RawValue, c.RawUnit, config.CalorificValue) : c.Kwh)).ToList();
        var agreements = await db.Set<Agreement>().AsNoTracking().Where(a => a.Fuel == fuel && a.ValidFromUtc < extendedEnd && (a.ValidToUtc == null || a.ValidToUtc > extendedStart)).ToListAsync(ct);
        var rates = await db.Set<UnitRate>().AsNoTracking().Where(a => a.Fuel == fuel && a.ValidFromUtc < extendedEnd && (a.ValidToUtc == null || a.ValidToUtc > extendedStart)).ToListAsync(ct);
        var standing = await db.Set<StandingCharge>().AsNoTracking().Where(a => a.Fuel == fuel && a.ValidFromUtc < end && (a.ValidToUtc == null || a.ValidToUtc > start)).ToListAsync(ct);
        var dispatches = await db.Set<Dispatch>().AsNoTracking().Where(d => d.StartUtc < extendedEnd && d.EndUtc > extendedStart).Select(d => new DispatchWindow(d.StartUtc, d.EndUtc, d.DeviceId, d.IsBoost)).ToListAsync(ct);
        return new PricingEngine().PriceDay(new(day, fuel, consumption, agreements, rates, standing, dispatches, config.Pricing));
    }
    public async Task RepriceAsync(LocalDate day, CancellationToken ct)
    {
        var results = new Dictionary<Fuel, PricedDayResult>();
        foreach (var fuel in Enum.GetValues<Fuel>()) results[fuel] = await ComputeAsync(day, fuel, ct);
        var (start, end) = EnergyTime.Bounds(day); var date = EnergyTime.Date(day); var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Set<PricedSlot>().Where(s => s.SlotStartUtc >= start && s.SlotStartUtc < end).ExecuteDeleteAsync(ct);
        await db.Set<PricedDay>().Where(s => s.LocalDate == date).ExecuteDeleteAsync(ct);
        foreach (var (fuel, result) in results)
        {
            db.AddRange(result.Slots.Select(s => new PricedSlot { Fuel = fuel, SlotStartUtc = s.StartUtc, Kwh = s.Kwh, RatePence = s.RatePence, Band = s.Band, RateSource = s.RateSource, CostPence = s.CostPence, TariffCode = s.TariffCode, PricedUtc = now }));
            db.Add(new PricedDay { Fuel = fuel, LocalDate = date, EnergyCostPence = result.EnergyCostPence, StandingPence = result.StandingPence, SlotCount = result.Slots.Count, MissingSlots = result.MissingSlots, UnpricedSlots = result.UnpricedSlots, WarningsJson = JsonSerializer.Serialize(result.Warnings), PricedUtc = now });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }
    public async Task RepriceRecentAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); var today = EnergyTime.LocalDay(DateTime.UtcNow);
        // Re-price retained history when settings/rates change; correctness before premature optimization.
        for (var day = today.PlusDays(-config.HistoryDays); day <= today; day = day.PlusDays(1)) await RepriceAsync(day, ct);
    }
}

using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnergyDash.Infrastructure;

public sealed class IngestionService(EnergyDbContext db, OctopusRestClient octopus, OctopusGraphClient graph, IBatteryProvider battery, SettingsStore settings)
{
    public async Task AccountAsync(CancellationToken ct)
    {
        var account = await octopus.AccountAsync(ct); var config = await settings.GetAsync(ct);
        // Discover both fuels atomically; failed discovery must not leave partial agreements.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var meters = (await db.Set<Meter>().ToListAsync(ct)).ToDictionary(m => (m.Fuel, m.MpanMprn, m.Serial));
        var agreements = (await db.Set<Agreement>().ToListAsync(ct)).ToDictionary(a => (a.Fuel, a.TariffCode, a.ValidFromUtc));
        try
        {
        foreach (var property in account.Properties)
            foreach (var (fuel, points) in new[] { (Fuel.Elec, property.ElectricityMeterPoints), (Fuel.Gas, property.GasMeterPoints) })
                foreach (var point in points)
                {
                    foreach (var meter in point.Meters)
                    {
                        var number = point.Mpan ?? point.Mprn ?? throw new InvalidOperationException("Missing meter point number.");
                        var key = (fuel, number, meter.SerialNumber);
                        if (!meters.TryGetValue(key, out var row)) { row = new Meter { Fuel = fuel, MpanMprn = number, Serial = meter.SerialNumber }; db.Add(row); meters.Add(key, row); }
                        row.IsExport = point.IsExport; row.Unit = fuel == Fuel.Gas ? config.GasUnit : "kWh";
                    }
                    if (!point.IsExport)
                        foreach (var agreement in point.Agreements)
                        {
                            var product = OctopusRestClient.ProductCode(agreement.TariffCode);
                            var key = (fuel, agreement.TariffCode, agreement.ValidFrom);
                            if (!agreements.TryGetValue(key, out var row)) { row = new Agreement { Fuel = fuel, TariffCode = agreement.TariffCode, ValidFromUtc = agreement.ValidFrom }; db.Add(row); agreements.Add(key, row); }
                            row.ProductCode = product; row.ValidToUtc = agreement.ValidTo;
                        }
                }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        }
        catch { db.ChangeTracker.Clear(); throw; }
    }
    public async Task RatesAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); var start = DateTime.UtcNow.AddDays(-config.HistoryDays - 2); var end = DateTime.UtcNow.AddDays(2);
        var agreements = await db.Set<Agreement>().Where(a => a.ValidToUtc == null || a.ValidToUtc > start).ToListAsync(ct);
        foreach (var a in agreements)
        {
            foreach (var (register, rate) in await octopus.UnitRatesAsync(a, start, end, ct))
            {
                var row = await db.Set<UnitRate>().SingleOrDefaultAsync(r => r.Fuel == a.Fuel && r.TariffCode == a.TariffCode && r.Register == register && r.ValidFromUtc == rate.ValidFrom, ct);
                if (row is null) { row = new UnitRate { Fuel = a.Fuel, TariffCode = a.TariffCode, Register = register, ValidFromUtc = rate.ValidFrom }; db.Add(row); }
                row.ValidToUtc = rate.ValidTo; row.PenceIncVat = rate.ValueIncVat;
            }
            foreach (var rate in await octopus.RatesAsync(a, "standing-charges", start, end, ct))
            {
                var row = await db.Set<StandingCharge>().SingleOrDefaultAsync(r => r.Fuel == a.Fuel && r.TariffCode == a.TariffCode && r.ValidFromUtc == rate.ValidFrom, ct);
                if (row is null) { row = new StandingCharge { Fuel = a.Fuel, TariffCode = a.TariffCode, ValidFromUtc = rate.ValidFrom }; db.Add(row); }
                row.ValidToUtc = rate.ValidTo; row.PencePerDayIncVat = rate.ValueIncVat;
            }
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task ConsumptionAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); var today = EnergyTime.LocalDay(DateTime.UtcNow);
        var populatedFuels = await db.Set<Consumption>().Select(c => c.Fuel).Distinct().ToListAsync(ct);
        var end = EnergyTime.Bounds(today).End;
        var meters = await db.Set<Meter>().Where(m => !m.IsExport).ToListAsync(ct);
        var readings = new Dictionary<(Fuel, DateTime), (OctopusConsumption Reading, string Unit)>();
        foreach (var meter in meters)
        {
            var start = EnergyTime.Bounds(today.PlusDays(populatedFuels.Contains(meter.Fuel) ? -7 : -config.HistoryDays)).Start;
            foreach (var reading in await octopus.ConsumptionAsync(meter, start, end, ct))
            {
                var key = (meter.Fuel, reading.IntervalStart);
                // Historical meter replacements may have disjoint ranges. Overlapping imports are ambiguous.
                if (readings.TryGetValue(key, out var prior) && prior.Reading != reading) throw new InvalidOperationException("Overlapping import-meter readings require account selection; no ambiguous consumption was saved.");
                readings[key] = (reading, meter.Fuel == Fuel.Gas ? config.GasUnit : "kWh");
            }
        }
        await UpsertConsumptionAsync(readings.Select(p => new Consumption { Fuel = p.Key.Item1, IntervalStartUtc = p.Value.Reading.IntervalStart, IntervalEndUtc = p.Value.Reading.IntervalEnd, RawValue = p.Value.Reading.Consumption, RawUnit = p.Value.Unit, Kwh = GasConversion.ToKwh(p.Value.Reading.Consumption, p.Value.Unit, config.CalorificValue), FetchedUtc = DateTime.UtcNow }), ct);
    }
    public async Task UpsertConsumptionAsync(IEnumerable<Consumption> values, CancellationToken ct)
    {
        var incoming = values.ToArray(); if (incoming.Length == 0) return;
        var start = incoming.Min(x => x.IntervalStartUtc); var end = incoming.Max(x => x.IntervalStartUtc);
        var existing = (await db.Set<Consumption>().Where(x => x.IntervalStartUtc >= start && x.IntervalStartUtc <= end).ToListAsync(ct)).ToDictionary(x => (x.Fuel, x.IntervalStartUtc));
        foreach (var row in incoming)
            if (existing.TryGetValue((row.Fuel, row.IntervalStartUtc), out var old)) db.Entry(old).CurrentValues.SetValues(row);
            else { db.Add(row); existing.Add((row.Fuel, row.IntervalStartUtc), row); }
        await db.SaveChangesAsync(ct);
    }
    public async Task DispatchesAsync(CancellationToken ct)
    {
        foreach (var dispatch in await graph.DispatchesAsync(ct))
        {
            if (dispatch.EndUtc <= dispatch.StartUtc) continue;
            var existing = await db.Set<Dispatch>().SingleOrDefaultAsync(d => d.StartUtc == dispatch.StartUtc && d.EndUtc == dispatch.EndUtc && d.Source == dispatch.Source && d.DeviceId == dispatch.DeviceId, ct);
            if (existing is null) db.Add(dispatch);
            else { existing.LastSeenUtc = DateTime.UtcNow; existing.DeltaKwh = dispatch.DeltaKwh; existing.IsBoost = dispatch.IsBoost; }
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task TeslaHistoryAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); if (string.IsNullOrWhiteSpace(config.TeslaSiteId)) throw new InvalidOperationException("Select a Tesla energy site in Settings.");
        var today = EnergyTime.LocalDay(DateTime.UtcNow);
        foreach (var row in await battery.HistoryAsync(config.TeslaSiteId, EnergyTime.Bounds(today.PlusDays(-1)).Start, EnergyTime.Bounds(today).End, ct))
        {
            var existing = await db.Set<BatteryInterval>().FindAsync([row.IntervalStartUtc], ct);
            if (existing is null) db.Add(row); else db.Entry(existing).CurrentValues.SetValues(row);
        }
        await db.SaveChangesAsync(ct);
    }
}

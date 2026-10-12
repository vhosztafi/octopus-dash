using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnergyDash.Infrastructure;

public sealed class IngestionService(EnergyDbContext db, OctopusRestClient octopus, OctopusGraphClient graph, IBatteryProvider battery, SettingsStore settings)
{
    public async Task AccountAsync(CancellationToken ct)
    {
        var account = await octopus.AccountAsync(ct); var config = await settings.GetAsync(ct);
        foreach (var property in account.Properties)
            foreach (var (fuel, points) in new[] { (Fuel.Elec, property.ElectricityMeterPoints), (Fuel.Gas, property.GasMeterPoints) })
                foreach (var point in points)
                {
                    foreach (var meter in point.Meters)
                    {
                        var number = point.Mpan ?? point.Mprn ?? throw new InvalidOperationException("Missing meter point number.");
                        var row = await db.Set<Meter>().SingleOrDefaultAsync(x => x.Fuel == fuel && x.MpanMprn == number && x.Serial == meter.SerialNumber, ct);
                        if (row is null) { row = new Meter { Fuel = fuel, MpanMprn = number, Serial = meter.SerialNumber }; db.Add(row); }
                        row.IsExport = point.IsExport; row.Unit = fuel == Fuel.Gas ? config.GasUnit : "kWh";
                    }
                    if (!point.IsExport)
                        foreach (var agreement in point.Agreements)
                        {
                            var row = await db.Set<Agreement>().SingleOrDefaultAsync(x => x.Fuel == fuel && x.TariffCode == agreement.TariffCode && x.ValidFromUtc == agreement.ValidFrom, ct);
                            if (row is null) { row = new Agreement { Fuel = fuel, TariffCode = agreement.TariffCode, ValidFromUtc = agreement.ValidFrom }; db.Add(row); }
                            row.ProductCode = OctopusRestClient.ProductCode(agreement.TariffCode); row.ValidToUtc = agreement.ValidTo;
                        }
                }
        await db.SaveChangesAsync(ct);
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
        var first = !await db.Set<Consumption>().AnyAsync(ct);
        var start = EnergyTime.Bounds(today.PlusDays(first ? -config.HistoryDays : -7)).Start; var end = EnergyTime.Bounds(today).End;
        var meters = await db.Set<Meter>().Where(m => !m.IsExport).ToListAsync(ct);
        var readings = new Dictionary<(Fuel, DateTime), (OctopusConsumption Reading, string Unit)>();
        foreach (var meter in meters)
            foreach (var reading in await octopus.ConsumptionAsync(meter, start, end, ct))
            {
                var key = (meter.Fuel, reading.IntervalStart);
                // Historical meter replacements may have disjoint ranges. Overlapping imports are ambiguous.
                if (readings.TryGetValue(key, out var prior) && prior.Reading != reading) throw new InvalidOperationException("Overlapping import-meter readings require account selection; no ambiguous consumption was saved.");
                readings[key] = (reading, meter.Fuel == Fuel.Gas ? config.GasUnit : "kWh");
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

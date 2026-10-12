using System.Text.Json;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;

namespace EnergyDash.Infrastructure;

public sealed class TelemetryService(EnergyDbContext db, SettingsStore settings, IBatteryProvider battery, AlertService alerts)
{
    public async Task PollAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); if (string.IsNullOrWhiteSpace(config.TeslaSiteId)) throw new InvalidOperationException("Select a Tesla energy site.");
        var live = await battery.LiveAsync(config.TeslaSiteId, ct);
        var current = new PowerSample(live.SyncedUtc, live.Status.GridPower);
        var previousRow = await db.Set<Setting>().FindAsync(["previousPowerSample"], ct);
        var previous = previousRow is null ? null : JsonSerializer.Deserialize<PowerSample>(previousRow.ValueJson);
        var slotStart = EnergyTime.SlotStart(current.AtUtc);
        if (previous is not null)
        {
            foreach (var contribution in LiveIntegration.Between(previous, current))
            {
                var slot = await db.Set<LiveAccumulator>().FindAsync([contribution.SlotStartUtc], ct);
                if (slot is null) { slot = new LiveAccumulator { SlotStartUtc = contribution.SlotStartUtc, HasGap = previous.AtUtc > contribution.SlotStartUtc.AddSeconds(90) }; db.Add(slot); }
                slot.ImportWh += contribution.Wh; slot.Samples++; slot.UpdatedUtc = current.AtUtc;
            }
        }
        var currentAccumulator = await db.Set<LiveAccumulator>().FindAsync([slotStart], ct);
        if (currentAccumulator is null) { currentAccumulator = new LiveAccumulator { SlotStartUtc = slotStart, HasGap = current.AtUtc > slotStart.AddSeconds(90), UpdatedUtc = current.AtUtc }; db.Add(currentAccumulator); }
        if (previous is null || current.AtUtc - previous.AtUtc > TimeSpan.FromMinutes(2)) currentAccumulator.HasGap = true;
        if (previousRow is null) { previousRow = new Setting { Key = "previousPowerSample" }; db.Add(previousRow); }
        previousRow.ValueJson = JsonSerializer.Serialize(current);
        var liveRow = await db.Set<Setting>().FindAsync(["batteryLive"], ct);
        if (liveRow is null) { liveRow = new Setting { Key = "batteryLive" }; db.Add(liveRow); }
        liveRow.ValueJson = JsonSerializer.Serialize(live);
        await db.SaveChangesAsync(ct);
        await alerts.LiveAsync(slotStart, currentAccumulator.ImportWh / 1000m, currentAccumulator.HasGap, ct);
    }
}

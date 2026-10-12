using System.Globalization;
using System.Net;
using System.Text.Json;
using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;

namespace EnergyDash.Infrastructure;

public sealed class AlertService(EnergyDbContext db, SettingsStore settings, PricingService pricing, IEnumerable<INotifier> notifiers)
{
    public async Task LiveAsync(DateTime slot, decimal kwh, bool gap, CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); if (!config.PeakAlertEnabled || config.PeakSource != "tesla" || gap) return;
        var result = await pricing.ComputeAsync(EnergyTime.LocalDay(slot), Fuel.Elec, ct); var priced = result.Slots.Single(s => s.StartUtc == slot);
        if (priced.RatePence is null || !AlertMath.PeakEligible(priced.Band, config)) return;
        if (kwh > config.PeakLimitKwh) await FireAsync("PeakSlot", slot.ToString("O"), kwh, config.PeakLimitKwh, "Peak-time alert", config, ct);
        else if (config.EarlyWarning && kwh > config.PeakLimitKwh * .8m) await FireAsync("PeakEarly", slot.ToString("O"), kwh, config.PeakLimitKwh * .8m, "Peak-time early warning", config, ct);
    }
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        var start = DateTime.UtcNow.AddDays(-7);
        var slots = await db.Set<PricedSlot>().AsNoTracking().Where(s => s.Fuel == Fuel.Elec && s.SlotStartUtc >= start && s.Kwh != null && s.RatePence != null).ToListAsync(ct);
        if (config.PeakAlertEnabled)
            foreach (var slot in slots.Where(s => AlertMath.PeakEligible(s.Band, config) && s.Kwh > config.PeakLimitKwh))
            {
                var subject = slot.SlotStartUtc.ToString("O");
                var existing = await db.Set<AlertEvent>().SingleOrDefaultAsync(e => e.SubjectKey == subject && db.Set<AlertRule>().Any(r => r.Id == e.RuleId && r.Kind == "PeakSlot"), ct);
                if (existing is not null)
                {
                    existing.ReconciledValue = slot.Kwh; await db.SaveChangesAsync(ct);
                    if (config.RenotifyReconciliation && existing.Value != slot.Kwh) await FireAsync("PeakReconciliation", subject, slot.Kwh!.Value, config.PeakLimitKwh, "Peak alert reconciliation", config, ct);
                }
                else await FireAsync("PeakSlot", subject, slot.Kwh!.Value, config.PeakLimitKwh, "Metered peak-time alert", config, ct);
            }
        // Record lower readings as well, so every live mismatch is available to inspect.
        var events = await db.Set<AlertEvent>().Where(e => e.FiredUtc >= start).ToListAsync(ct);
        foreach (var ev in events) if (DateTime.TryParse(ev.SubjectKey, null, DateTimeStyles.RoundtripKind, out var slotTime)) ev.ReconciledValue = slots.FirstOrDefault(s => s.SlotStartUtc == slotTime)?.Kwh;
        await db.SaveChangesAsync(ct);
        if (!config.GasAlertEnabled) return;
        var dates = await db.Set<PricedDay>().AsNoTracking().Where(d => d.Fuel == Fuel.Gas && d.MissingSlots == 0 && d.UnpricedSlots == 0 && d.StandingPence != null).ToListAsync(ct);
        foreach (var day in dates.Where(d => d.LocalDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)) && d.EnergyCostPence + d.StandingPence > config.GasDailyLimitPounds * 100))
            await FireAsync("GasDaily", day.LocalDate.ToString("yyyy-MM-dd"), (day.EnergyCostPence + day.StandingPence!.Value) / 100, config.GasDailyLimitPounds, "Daily gas cost alert", config, ct);
    }
    private async Task FireAsync(string kind, string subject, decimal value, decimal threshold, string title, DashboardSettings config, CancellationToken ct)
    {
        var rule = await db.Set<AlertRule>().SingleOrDefaultAsync(r => r.Kind == kind, ct);
        if (rule is null) { rule = new AlertRule { Kind = kind, Enabled = true, ParamsJson = JsonSerializer.Serialize(new { threshold }) }; db.Add(rule); await db.SaveChangesAsync(ct); }
        if (await db.Set<AlertEvent>().AnyAsync(e => e.RuleId == rule.Id && e.SubjectKey == subject, ct)) return;
        var now = DateTime.UtcNow; var cooldown = now.AddMinutes(-config.CooldownMinutes);
        var suppressed = config.CooldownMinutes > 0 && await db.Set<AlertEvent>().AnyAsync(e => e.RuleId == rule.Id && e.FiredUtc >= cooldown, ct);
        var ev = new AlertEvent { RuleId = rule.Id, SubjectKey = subject, FiredUtc = now, Value = value, Threshold = threshold, DeliveryJson = suppressed ? "{\"status\":\"cooldown\"}" : "{\"status\":\"pending\"}" };
        db.Add(ev); await db.SaveChangesAsync(ct); // Persist dedupe before any external side effect.
        if (suppressed) return;
        var body = $"{title}: {value.ToString("0.###", CultureInfo.InvariantCulture)} > {threshold.ToString("0.###", CultureInfo.InvariantCulture)}. {subject}";
        var results = new Dictionary<string, string>();
        foreach (var notifier in notifiers.Where(n => n.Channel == "email" ? config.NotifyEmail : config.NotifyPush))
        {
            try { await notifier.SendAsync(new(title, body, "<p>" + WebUtility.HtmlEncode(body) + "</p>", config.Recipients), ct); results[notifier.Channel] = "sent"; }
            catch (Exception ex) when (ex is not OperationCanceledException) { results[notifier.Channel] = "failed: " + ex.GetType().Name; }
        }
        ev.DeliveryJson = JsonSerializer.Serialize(results); await db.SaveChangesAsync(ct);
    }
}

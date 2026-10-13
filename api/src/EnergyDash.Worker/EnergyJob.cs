using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace EnergyDash.Worker;

[DisallowConcurrentExecution]
public sealed class EnergyJob(EnergyDbContext db, IngestionService ingest, PricingService pricing, TelemetryService telemetry, AlertService alerts, ReportService reports, SettingsStore settings, ILogger<EnergyJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken; var kind = context.MergedJobDataMap.GetString("kind") ?? "Sync";
        try { await RunAsync(kind, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError("Energy job {Kind} failed ({ExceptionType}); details withheld to protect credentials", kind, ex.GetType().Name); }
    }
    private async Task LoggedAsync(string source, Func<Task> action, CancellationToken ct)
    {
        var log = new SyncLog { Source = source, StartedUtc = DateTime.UtcNow }; db.Add(log); await db.SaveChangesAsync(ct);
        try { await action(); log.Ok = true; log.Detail = "Completed"; }
        catch (Exception ex) when (ex is not OperationCanceledException) { db.ChangeTracker.Clear(); log.Detail = "Failed: " + ex.GetType().Name; throw; }
        finally
        {
            // Pricing/token refresh clears the tracker. Reattach only this log.
            if (db.Entry(log).State == EntityState.Detached) db.Attach(log);
            log.FinishedUtc = DateTime.UtcNow; db.Entry(log).State = EntityState.Modified; await db.SaveChangesAsync(CancellationToken.None);
        }
    }
    private async Task RunAsync(string kind, CancellationToken ct)
    {
        if (kind == "Commands") { await CommandsAsync(ct); return; }
        if (kind == "Sync")
        {
            // A supplier's outage does not prevent the other source from updating.
            foreach (var task in new[] { "OctopusAccount", "OctopusRates", "OctopusConsumption", "Dispatch", "TeslaHistory" })
                try { await RunAsync(task, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Sync component {Kind} failed ({ExceptionType})", task, ex.GetType().Name); }
            return;
        }
        await LoggedAsync(kind, async () =>
        {
            switch (kind)
            {
                case "OctopusAccount": await ingest.AccountAsync(ct); break;
                case "OctopusRates": await ingest.RatesAsync(ct); await PriceAndAlertsAsync(ct); break;
                case "OctopusConsumption": await ingest.ConsumptionAsync(ct); await PriceAndAlertsAsync(ct); break;
                case "Dispatch": await ingest.DispatchesAsync(ct); await PriceAndAlertsAsync(ct); break;
                case "TeslaHistory": await ingest.TeslaHistoryAsync(ct); break;
                case "TeslaLive": await telemetry.PollAsync(ct); break;
                case "Pricing": case "Reprice": await PriceAndAlertsAsync(ct); break;
                case "WeeklyReport": await ScheduledReportAsync(ct); break;
            }
        }, ct);
    }
    private async Task PriceAndAlertsAsync(CancellationToken ct) { await pricing.RepriceRecentAsync(ct); await alerts.ReconcileAsync(ct); }
    private async Task ScheduledReportAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct); var local = EnergyTime.Instant(DateTime.UtcNow).InZone(EnergyTime.London);
        if (!config.WeeklyEnabled || config.WeekDay != local.DayOfWeek.ToString()[..3].ToLowerInvariant() || string.CompareOrdinal($"{local.Hour:00}:{local.Minute:00}", config.WeekTime) < 0) return;
        var thisMonday = local.Date.PlusDays(1 - (int)local.DayOfWeek);
        // Always last fully completed Mon–Sun; Sunday evening cannot include tomorrow's meter data.
        var previousMonday = thisMonday.PlusDays(-7); var date = EnergyTime.Date(previousMonday);
        var existing = await db.Set<ReportRun>().AsNoTracking().SingleOrDefaultAsync(r => r.WeekStartLocal == date, ct);
        if (existing is not null && existing.Status is "Sent" or "Sending" or "Failed") return;
        await reports.SendAsync(previousMonday.ToString("yyyy-MM-dd", null), ct);
    }
    private async Task CommandsAsync(CancellationToken ct)
    {
        // Single-worker queue; reclaim interrupted commands after 30 minutes.
        var stale = DateTime.UtcNow.AddMinutes(-30);
        var command = await db.Set<SyncCommand>().OrderBy(c => c.CreatedUtc).FirstOrDefaultAsync(c => c.CompletedUtc == null && (c.ClaimedUtc == null || c.ClaimedUtc < stale), ct);
        if (command is null) return;
        command.ClaimedUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        try { if (command.Kind == "Report") await reports.SendAsync(command.Argument!, ct); else await RunAsync(command.Kind, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { command.Error = "Failed: " + ex.GetType().Name; }
        finally { if (db.Entry(command).State == EntityState.Detached) db.Attach(command); command.CompletedUtc = DateTime.UtcNow; db.Entry(command).State = EntityState.Modified; await db.SaveChangesAsync(CancellationToken.None); }
    }
}

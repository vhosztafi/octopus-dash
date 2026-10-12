using System.Text;
using EnergyDash.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyDash.Infrastructure;

public sealed class ReportService(EnergyDbContext db, DashboardQueries queries, SettingsStore settings, IEnumerable<INotifier> notifiers, IServiceProvider services, ILoggerFactory loggerFactory)
{
    public async Task SendAsync(string monday, CancellationToken ct)
    {
        // Serialize scheduled and manually queued sends; mark Sending durably before SMTP.
        await db.Database.OpenConnectionAsync(ct);
        var resource = "EnergyDash.Weekly." + monday;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 5000; IF @result < 0 THROW 50001, 'Weekly report is already running', 1;", ct);
            await SendCoreAsync(monday, ct);
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_releaseapplock @Resource = {resource}, @LockOwner = 'Session';", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }
    private async Task SendCoreAsync(string monday, CancellationToken ct)
    {
        var day = EnergyTime.ParseDay(monday); var date = EnergyTime.Date(day); var config = await settings.GetAsync(ct);
        var run = await db.Set<ReportRun>().SingleOrDefaultAsync(r => r.WeekStartLocal == date, ct);
        if (run is not null && run.Status is "Sent" or "Sending") return;
        if (run is null) { run = new ReportRun { WeekStartLocal = date }; db.Add(run); }
        var report = await queries.WeekAsync(monday, ct);
        if (!report.Complete) { run.Status = "Incomplete"; run.Error = "Week has missing readings or prices; delivery deferred."; await db.SaveChangesAsync(ct); return; }
        run.Status = "Sending"; run.Error = null; await db.SaveChangesAsync(ct);
        try
        {
            await using var renderer = new HtmlRenderer(services, loggerFactory);
            var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<WeeklyEmail>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Report"] = report }))).ToHtmlString());
            string? csv = null;
            if (config.AttachCsv)
            {
                var rows = new StringBuilder();
                for (var i = 0; i < 7; i++) { var ledger = Ledger.From(await queries.DayAsync(day.PlusDays(i).ToString("yyyy-MM-dd", null), ct), "all"); var part = Ledger.Csv(ledger); rows.AppendLine(i == 0 ? part : part[(part.IndexOf('\n') + 1)..]); }
                csv = rows.ToString();
            }
            await notifiers.Single(n => n.Channel == "email").SendAsync(new("Home energy · week of " + monday, $"Week total £{report.Kpis.TotalPence / 100:0.00}", html, config.Recipients, csv), ct);
            run.Status = "Sent"; run.SentUtc = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { run.Status = "Failed"; run.Error = "Delivery failed: " + ex.GetType().Name; }
        await db.SaveChangesAsync(ct);
    }
}

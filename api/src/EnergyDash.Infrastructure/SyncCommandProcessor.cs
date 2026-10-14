using EnergyDash.Core;
using Microsoft.EntityFrameworkCore;

namespace EnergyDash.Infrastructure;

public sealed class SyncCommandProcessor(EnergyDbContext db)
{
    // Caller holds the shared EnergyPipeline SQL lease. Abruptly killed processes are reclaimed after 30m.
    public async Task ExecuteNextAsync(Func<SyncCommand, CancellationToken, Task> execute, CancellationToken ct)
    {
        var stale = DateTime.UtcNow.AddMinutes(-30);
        var command = await db.Set<SyncCommand>().OrderBy(c => c.CreatedUtc).FirstOrDefaultAsync(c => c.CompletedUtc == null && (c.ClaimedUtc == null || c.ClaimedUtc < stale), ct);
        if (command is null) return;
        command.ClaimedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var finished = false;
        try { await execute(command, ct); finished = true; }
        catch (Exception ex) when (ex is not OperationCanceledException) { command.Error = "Failed: " + ex.GetType().Name; finished = true; }
        finally
        {
            // Graceful IIS shutdown must not mark interrupted imports/reports as completed.
            db.ChangeTracker.Clear();
            command.CompletedUtc = finished ? DateTime.UtcNow : null;
            if (!finished) command.ClaimedUtc = null;
            db.Attach(command); db.Entry(command).State = EntityState.Modified;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}

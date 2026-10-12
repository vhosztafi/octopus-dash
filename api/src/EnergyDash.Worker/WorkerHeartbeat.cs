using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnergyDash.Worker;

public sealed class WorkerHeartbeat(IServiceScopeFactory scopes, ILogger<WorkerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try { using var scope = scopes.CreateScope(); if (await scope.ServiceProvider.GetRequiredService<EnergyDbContext>().Database.CanConnectAsync(stoppingToken)) await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "energy-worker-heartbeat"), DateTime.UtcNow.ToString("O"), stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Worker health check failed ({ExceptionType})", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

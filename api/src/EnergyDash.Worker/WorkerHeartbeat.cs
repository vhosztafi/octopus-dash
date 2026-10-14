using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnergyDash.Worker;

public sealed class WorkerHeartbeat(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<WorkerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<EnergyDbContext>().Database.CanConnectAsync(stoppingToken))
                {
                    // A fresh marker proves shared encrypted supplier credentials can be read as this service identity.
                    var secrets = scope.ServiceProvider.GetRequiredService<SecretStore>();
                    await secrets.GetStoredAsync("Octopus__ApiKey", stoppingToken);
                    await secrets.GetStoredAsync("Tesla__RefreshToken", stoppingToken);
                    var path = configuration["Worker:HeartbeatPath"] ?? Path.Combine(Path.GetTempPath(), "energy-worker-heartbeat");
                    await File.WriteAllTextAsync(path, DateTime.UtcNow.ToString("O"), stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Worker health check failed ({ExceptionType})", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

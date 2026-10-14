using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Testcontainers.MsSql;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class SchedulingTests
{
    [Fact]
    public async Task RegistrationPreservesLondonSchedulesAndStartupCatchup()
    {
        using var services = new ServiceCollection().AddLogging().AddEnergyScheduling().BuildServiceProvider();
        var scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try
        {
            var startup = await scheduler.GetTrigger(new TriggerKey("Startup"));
            Assert.NotNull(startup);
            Assert.Equal("EnergyPipeline", startup.JobKey.Name);
            Assert.Equal("Sync", startup.JobDataMap.GetString("kind"));
            var rates = Assert.IsAssignableFrom<ICronTrigger>(await scheduler.GetTrigger(new TriggerKey("Rates")));
            Assert.Equal("0 10 3 * * ?", rates.CronExpressionString);
            Assert.Equal("Europe/London", rates.TimeZone.Id);
            var pipeline = await scheduler.GetJobDetail(new JobKey("EnergyPipeline"));
            Assert.True(pipeline!.ConcurrentExecutionDisallowed);
            Assert.NotEqual((await scheduler.GetTrigger(new TriggerKey("LiveCron")))!.JobKey, pipeline.Key);
            Assert.NotEqual((await scheduler.GetTrigger(new TriggerKey("ReportCron")))!.JobKey, pipeline.Key);
        }
        finally { await scheduler.Shutdown(); }
    }

    [SqlFact]
    public async Task OverlappingHostsExcludeSameGroupAndInterruptedCommandsRemainRetryable()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
        await sql.StartAsync();
        var connection = sql.GetConnectionString();
        await using var db = new EnergyDbContext(new DbContextOptionsBuilder<EnergyDbContext>().UseSqlServer(connection).Options);
        await db.Database.MigrateAsync();
        var firstHost = await JobExecutionLease.TryAcquireAsync(connection, "EnergyPipeline", default);
        Assert.NotNull(firstHost);
        try
        {
            Assert.Null(await JobExecutionLease.TryAcquireAsync(connection, "EnergyPipeline", default));
            await using var independentLiveJob = await JobExecutionLease.TryAcquireAsync(connection, "LiveTelemetry", default);
            Assert.NotNull(independentLiveJob);
        }
        finally { await firstHost.DisposeAsync(); }
        await using var nextHost = await JobExecutionLease.TryAcquireAsync(connection, "EnergyPipeline", default);
        Assert.NotNull(nextHost);

        var queued = new SyncCommand { Kind = "Sync", CreatedUtc = DateTime.UtcNow };
        db.Add(queued); await db.SaveChangesAsync();
        var processor = new SyncCommandProcessor(db);
        using var stopping = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ExecuteNextAsync((_, ct) =>
        {
            stopping.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask;
        }, stopping.Token));
        db.ChangeTracker.Clear();
        var interrupted = await db.Set<SyncCommand>().SingleAsync();
        Assert.Null(interrupted.CompletedUtc); Assert.Null(interrupted.ClaimedUtc); Assert.Null(interrupted.Error);
        var calls = 0;
        await processor.ExecuteNextAsync((_, _) => { calls++; db.ChangeTracker.Clear(); return Task.CompletedTask; }, default);
        await processor.ExecuteNextAsync((_, _) => { calls++; return Task.CompletedTask; }, default);
        Assert.Equal(1, calls);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.Set<SyncCommand>().SingleAsync()).CompletedUtc);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace EnergyDash.Infrastructure;

public static class EnergyScheduling
{
    public static IServiceCollection AddEnergyScheduling(this IServiceCollection services)
    {
        services.AddScoped<SyncCommandProcessor>();
        services.AddQuartz(q =>
        {
            var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
            var pipeline = new JobKey("EnergyPipeline");
            q.AddJob<EnergyJob>(o => o.WithIdentity(pipeline).StoreDurably());
            void Trigger(string name, string cron, string kind) => q.AddTrigger(o => o.ForJob(pipeline).WithIdentity(name).UsingJobData("kind", kind).WithCronSchedule(cron, c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
            Trigger("Account", "0 0 3 * * ?", "OctopusAccount");
            Trigger("Rates", "0 10 3 * * ?", "OctopusRates");
            Trigger("MorningConsumption", "0 0 6-14 * * ?", "OctopusConsumption");
            Trigger("LaterConsumption", "0 0 2,18,22 * * ?", "OctopusConsumption");
            Trigger("Dispatch", "0 0/5 * * * ?", "Dispatch");
            Trigger("History", "0 15 * * * ?", "TeslaHistory");
            Trigger("Pricing", "0 30 3 * * ?", "Pricing");
            Trigger("Commands", "0/15 * * * * ?", "Commands");
            // Also catches up account/rates/consumption after the owner's daily 03:00 IIS recycle.
            q.AddTrigger(o => o.ForJob(pipeline).WithIdentity("Startup").UsingJobData("kind", "Sync").StartNow());
            var live = new JobKey("LiveTelemetry");
            q.AddJob<EnergyJob>(o => o.WithIdentity(live).UsingJobData("kind", "TeslaLive"));
            q.AddTrigger(o => o.ForJob(live).WithIdentity("LiveCron").WithCronSchedule("0 * * * * ?", c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
            q.AddTrigger(o => o.ForJob(live).WithIdentity("LiveStartup").StartNow());
            var report = new JobKey("ReportSchedule");
            q.AddJob<EnergyJob>(o => o.WithIdentity(report).UsingJobData("kind", "WeeklyReport"));
            q.AddTrigger(o => o.ForJob(report).WithIdentity("ReportCron").WithCronSchedule("0 * * * * ?", c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        return services;
    }
}

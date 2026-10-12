using EnergyDash.Infrastructure;
using EnergyDash.Worker;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog(c => c.Destructure.With<SecretRedactionPolicy>().MinimumLevel.Information().MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning).WriteTo.Console());
builder.Services.AddEnergyInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorkerHeartbeat>();
builder.Services.AddQuartz(q =>
{
    var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    // All orchestration uses one JobKey, preventing concurrent upserts/pricing across job kinds.
    var pipeline = new JobKey("EnergyPipeline"); q.AddJob<EnergyJob>(o => o.WithIdentity(pipeline).StoreDurably());
    void Trigger(string name, string cron, string kind) => q.AddTrigger(o => o.ForJob(pipeline).WithIdentity(name).UsingJobData("kind", kind).WithCronSchedule(cron, c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
    Trigger("Account", "0 0 3 * * ?", "OctopusAccount"); Trigger("Rates", "0 10 3 * * ?", "OctopusRates");
    Trigger("MorningConsumption", "0 0 6-14 * * ?", "OctopusConsumption"); Trigger("LaterConsumption", "0 0 2,18,22 * * ?", "OctopusConsumption");
    Trigger("Dispatch", "0 0/5 * * * ?", "Dispatch"); Trigger("History", "0 15 * * * ?", "TeslaHistory");
    Trigger("Pricing", "0 30 3 * * ?", "Pricing");
    var live = new JobKey("LiveTelemetry"); q.AddJob<EnergyJob>(o => o.WithIdentity(live).UsingJobData("kind", "TeslaLive"));
    q.AddTrigger(o => o.ForJob(live).WithIdentity("LiveCron").WithCronSchedule("0 * * * * ?", c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
    q.AddTrigger(o => o.ForJob(live).WithIdentity("LiveStartup").StartNow());
    // Check configured London day/time; this allows settings changes without process restart.
    var report = new JobKey("ReportSchedule"); q.AddJob<EnergyJob>(o => o.WithIdentity(report).UsingJobData("kind", "WeeklyReport"));
    q.AddTrigger(o => o.ForJob(report).WithIdentity("ReportCron").WithCronSchedule("0 * * * * ?", c => c.InTimeZone(london).WithMisfireHandlingInstructionDoNothing()));
    Trigger("Commands", "0/15 * * * * ?", "Commands");
    q.AddTrigger(o => o.ForJob(pipeline).WithIdentity("Startup").UsingJobData("kind", "Sync").StartNow());
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
await builder.Build().RunAsync();

using EnergyDash.Infrastructure;
using EnergyDash.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddPrivateServerConfiguration();
builder.Services.AddSerilog(c => c.Destructure.With<SecretRedactionPolicy>().MinimumLevel.Information().MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning).WriteTo.Console());
builder.Services.AddEnergyInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorkerHeartbeat>();
builder.Services.AddEnergyScheduling();
await builder.Build().RunAsync();

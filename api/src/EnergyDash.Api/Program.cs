using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using EnergyDash.Api;
using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddPrivateServerConfiguration();
var sample = builder.Configuration.GetValue("SampleMode", false);
var internalToken = builder.Configuration["INTERNAL_API_TOKEN"];
if (string.IsNullOrWhiteSpace(internalToken) || internalToken.Length < 32) throw new InvalidOperationException("INTERNAL_API_TOKEN must contain at least 32 characters.");
builder.Host.UseSerilog((_, logger) => logger.Destructure.With<SecretRedactionPolicy>().MinimumLevel.Information().MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning).WriteTo.Console());
builder.Services.AddProblemDetails(); builder.Services.AddOpenApi(o => o.AddSchemaTransformer((schema, context, _) =>
{
    if (context.JsonTypeInfo.Type == typeof(DashboardSettings) && schema.Properties is not null) schema.Required = schema.Properties.Keys.ToHashSet();
    return Task.CompletedTask;
}));
builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue<int?>("HttpsRedirection:HttpsPort"));
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict; });
builder.Services.AddEnergyInfrastructure(builder.Configuration, sample);
builder.Services.AddSingleton<SampleDashboard>();
if (sample) builder.Services.AddScoped<IDashboardData>(sp => sp.GetRequiredService<SampleDashboard>());
else builder.Services.AddScoped<IDashboardData>(sp => sp.GetRequiredService<DashboardQueries>());
builder.Services.AddScoped<SettingsValidator>();
var app = builder.Build();
if (builder.Configuration.GetValue("EnforceHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseExceptionHandler(error => error.Run(async ctx =>
{
    var exception = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is FormatException or ArgumentException or NodaTime.Text.UnparsableValueException ? 400 : exception is HttpRequestException ? 502 : 500;
    ctx.Response.StatusCode = status;
    await Results.Problem(statusCode: status, title: status == 400 ? "Invalid request" : status == 502 ? "Supplier connection failed" : "Operation failed", detail: "Check connection settings and server sync status. Secrets and supplier error payloads are omitted.").ExecuteAsync(ctx);
}));
app.Use(async (context, next) =>
{
    if (context.Request.Path != "/health")
    {
        var supplied = context.Request.Headers.Authorization.ToString();
        var expected = "Bearer " + internalToken;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)))) { await Results.Problem(statusCode: 401, title: "Unauthorized").ExecuteAsync(context); return; }
    }
    await next(context);
});
app.MapGet("/health", async (EnergyDbContext db, CancellationToken ct) => sample || await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapOpenApi();
var api = app.MapGroup("/api");
static bool ValidDay(string value) => NodaTime.Text.LocalDatePattern.Iso.Parse(value).Success;
static bool ValidWeek(string value) => ValidDay(value) && EnergyTime.ParseDay(value).DayOfWeek == NodaTime.IsoDayOfWeek.Monday;
static bool ValidFilter(string value) => value is "all" or "peak" or "off" or "disp" or "over";
api.MapGet("/status", (IDashboardData data, CancellationToken ct) => data.StatusAsync(ct)).Produces<StatusDto>();
api.MapGet("/days/latest-complete", (IDashboardData data, CancellationToken ct) => data.LatestAsync(ct)).Produces<LatestDayDto>();
api.MapGet("/days/{date}", async (string date, IDashboardData data, CancellationToken ct) => ValidDay(date) ? Results.Ok(await data.DayAsync(date, ct)) : Results.Problem(statusCode: 400, title: "Use YYYY-MM-DD")).Produces<DayDto>();
api.MapGet("/days/{date}/slots", async (string date, string? filter, IDashboardData data, CancellationToken ct) => ValidDay(date) && ValidFilter(filter ?? "all") ? Results.Ok(Ledger.From(await data.DayAsync(date, ct), filter ?? "all")) : Results.Problem(statusCode: 400, title: "Invalid date or filter")).Produces<LedgerDto>();
api.MapGet("/days/{date}/slots.csv", async (string date, string? filter, IDashboardData data, CancellationToken ct) => ValidDay(date) && ValidFilter(filter ?? "all") ? Results.File(Encoding.UTF8.GetBytes(Ledger.Csv(Ledger.From(await data.DayAsync(date, ct), filter ?? "all"))), "text/csv", $"half-hours-{date}.csv") : Results.Problem(statusCode: 400, title: "Invalid date or filter")).Produces(200, contentType: "text/csv");
api.MapGet("/weeks/{monday}", async (string monday, IDashboardData data, CancellationToken ct) => ValidWeek(monday) ? Results.Ok(await data.WeekAsync(monday, ct)) : Results.Problem(statusCode: 400, title: "Week must start on Monday")).Produces<WeekDto>();
api.MapGet("/powerwall/live", (IDashboardData data, CancellationToken ct) => data.LiveAsync(ct)).Produces<PowerwallDto>();
api.MapGet("/settings", async (SettingsStore store, SampleDashboard demo, CancellationToken ct) => sample ? demo.Settings : await store.GetAsync(ct)).Produces<DashboardSettings>();
api.MapPut("/settings", async (DashboardSettings value, SettingsValidator validator, SettingsStore store, SampleDashboard demo, EnergyDbContext db, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(value, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.Errors.GroupBy(e => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName)).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    if (sample) demo.Settings = value;
    else { await store.SaveAsync(value, ct); db.Add(new SyncCommand { Kind = "Reprice", CreatedUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); }
    return Results.Ok(value);
}).Produces<DashboardSettings>().ProducesValidationProblem();
api.MapGet("/connections", async (SecretStore secrets, IDashboardData data, CancellationToken ct) =>
{
    if (sample) return new ConnectionsDto(new("octopus", false, null, null, "Not connected · sample mode"), new("tesla", false, null, null, "Not connected · sample mode"));
    var octopus = await secrets.HintAsync("Octopus__ApiKey", ct); var tesla = await secrets.HintAsync("Tesla__RefreshToken", ct); var status = await data.StatusAsync(ct);
    return new ConnectionsDto(new("octopus", octopus is not null, octopus, await secrets.GetAsync("Octopus__AccountNumber", ct), octopus is null ? "Not connected" : status.OctopusHealth), new("tesla", tesla is not null, tesla, null, tesla is null ? "Not connected" : status.TeslaHealth));
}).Produces<ConnectionsDto>();
api.MapPut("/connections/octopus", async (OctopusCredentials value, SecretStore secrets, EnergyDbContext db, CancellationToken ct) =>
{
    if (sample) return Results.Problem(statusCode: 409, title: "Disable sample mode to save credentials");
    if (string.IsNullOrWhiteSpace(value.ApiKey) || value.ApiKey.Length > 500 || !System.Text.RegularExpressions.Regex.IsMatch(value.AccountNumber, "^A-[A-Za-z0-9]+$")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Provide a valid API key and A- account number."] });
    await secrets.PutAsync("Octopus__ApiKey", value.ApiKey, ct); await secrets.PutAsync("Octopus__AccountNumber", value.AccountNumber, ct);
    await secrets.PutAsync("Octopus__TokenExpires", DateTime.MinValue.ToString("O"), ct);
    db.Add(new SyncCommand { CreatedUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct);
    return Results.Ok(new ConnectionTestDto(true, "Credentials saved encrypted. Account discovery and initial sync queued."));
}).Produces<ConnectionTestDto>().ProducesValidationProblem();
api.MapPut("/connections/tesla", async (TeslaCredentials value, SecretStore secrets, CancellationToken ct) =>
{
    if (sample) return Results.Problem(statusCode: 409, title: "Disable sample mode to save credentials");
    if (string.IsNullOrWhiteSpace(value.RefreshToken) || string.IsNullOrWhiteSpace(value.ClientId) || value.RefreshToken.Length > 10000 || value.ClientId.Length > 500) return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Provide an authorized refresh token and client ID."] });
    await secrets.PutAsync("Tesla__RefreshToken", value.RefreshToken, ct); await secrets.PutAsync("Tesla__ClientId", value.ClientId, ct);
    if (!string.IsNullOrWhiteSpace(value.ClientSecret)) await secrets.PutAsync("Tesla__ClientSecret", value.ClientSecret, ct);
    // Invalidate cached access token so connection tests use the replacement credentials.
    await secrets.PutAsync("Tesla__Expires", DateTime.MinValue.ToString("O"), ct);
    return Results.Ok(new ConnectionTestDto(true, "OAuth credentials saved encrypted."));
}).Produces<ConnectionTestDto>().ProducesValidationProblem();
api.MapPost("/connections/tesla/authorize", async (TeslaAppCredentials value, TeslaAuthorization authorization, CancellationToken ct) =>
{
    if (sample) return Results.Problem(statusCode: 409, title: "Sample mode has no live supplier connection");
    return Results.Ok(await authorization.BeginAsync(value, ct));
}).Produces<TeslaAuthorizationStart>();
api.MapPost("/connections/tesla/callback", async (TeslaAuthorizationCallback value, TeslaAuthorization authorization, CancellationToken ct) =>
{
    if (sample) return Results.Problem(statusCode: 409, title: "Sample mode has no live supplier connection");
    await authorization.CompleteAsync(value, ct);
    return Results.Ok(new ConnectionTestDto(true, "Tesla authorization completed."));
}).Produces<ConnectionTestDto>();
api.MapPost("/connections/octopus/test", async (OctopusRestClient client, CancellationToken ct) => { if (sample) return Results.Problem(statusCode: 409, title: "Sample mode has no live supplier connection"); await client.AccountAsync(ct); return Results.Ok(new ConnectionTestDto(true, "Octopus account verified.")); }).Produces<ConnectionTestDto>();
api.MapPost("/connections/tesla/test", async (IBatteryProvider client, CancellationToken ct) => { if (sample) return Results.Problem(statusCode: 409, title: "Sample mode has no live supplier connection"); var sites = await client.SitesAsync(ct); return Results.Ok(new ConnectionTestDto(true, $"Tesla connected · {sites.Count} energy sites.")); }).Produces<ConnectionTestDto>();
api.MapGet("/tesla/sites", async (IBatteryProvider client, CancellationToken ct) => sample ? Array.Empty<EnergySiteDto>() : (await client.SitesAsync(ct)).Select(s => new EnergySiteDto(s.EnergySiteId?.ToString() ?? "", s.SiteName ?? "Powerwall")).ToArray()).Produces<IReadOnlyList<EnergySiteDto>>();
api.MapPost("/sync", async (EnergyDbContext db, CancellationToken ct) => { if (sample) return Results.Problem(statusCode: 409, title: "Disable sample mode to sync real data"); var command = new SyncCommand { CreatedUtc = DateTime.UtcNow }; db.Add(command); await db.SaveChangesAsync(ct); return Results.Accepted(value: new CommandDto(command.Id, "Queued")); }).Produces<CommandDto>(202);
api.MapPost("/reports/weekly/send", async (string week, EnergyDbContext db, CancellationToken ct) => { if (sample) return Results.Problem(statusCode: 409, title: "Sample reports are preview only"); if (!ValidWeek(week)) return Results.Problem(statusCode: 400, title: "Week must start on Monday"); var command = new SyncCommand { Kind = "Report", Argument = week, CreatedUtc = DateTime.UtcNow }; db.Add(command); await db.SaveChangesAsync(ct); return Results.Accepted(value: new CommandDto(command.Id, "Queued")); }).Produces<CommandDto>(202);
if (!sample && builder.Configuration.GetValue("Database:MigrateOnStartup", false)) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<EnergyDbContext>().Database.MigrateAsync(); }
app.Run();
public partial class Program;

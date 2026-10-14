using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Token = "contract-tests-internal-token-at-least-32-characters";
    public ApiFactory() { Environment.SetEnvironmentVariable("SampleMode", "true"); Environment.SetEnvironmentVariable("INTERNAL_API_TOKEN", Token); }
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["SampleMode"] = "true", ["INTERNAL_API_TOKEN"] = Token }));
    public HttpClient AuthorizedClient() { var client = CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", Token); return client; }
}
public sealed class ApiContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    public ApiContractTests(ApiFactory factory) => this.factory = factory;
    [Fact]
    public async Task UnauthorizedRequestsAreRejected()
    {
        using var client = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/status")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
    [Theory]
    [InlineData("/api/status", "sample")]
    [InlineData("/api/days/latest-complete", "date")]
    [InlineData("/api/days/2026-10-11", "slots")]
    [InlineData("/api/days/2026-10-11/slots?filter=over", "counts")]
    [InlineData("/api/weeks/2026-10-05", "days")]
    [InlineData("/api/powerwall/live", "chargePercent")]
    [InlineData("/api/settings", "peakLimitKwh")]
    [InlineData("/api/connections", "octopus")]
    public async Task ReadEndpointsHaveDocumentedShapes(string path, string property)
    {
        using var client = factory.AuthorizedClient(); using var response = await client.GetAsync(path); response.EnsureSuccessStatusCode(); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(json.TryGetProperty(property, out _));
    }
    [Fact]
    public async Task SampleDayMatchesPrototypeSnapshot()
    {
        using var client = factory.AuthorizedClient(); var day = await client.GetFromJsonAsync<DayDto>("/api/days/2026-10-11"); Assert.NotNull(day); Assert.True(day.Sample); Assert.Equal(48, day.Slots.Count); Assert.Equal(2, day.Alerts.Count); Assert.Equal(38.4m, day.Kpis.GasKwh); Assert.Equal(78m, day.Powerwall.ChargePercent); Assert.Equal(3, day.Slots.Count(s => s.Band == "disp"));
        // Exact snapshot of stable numerical contract, independent of locale/display rounding.
        var snapshot = JsonSerializer.Serialize(new { count = day.Slots.Count, import = day.Kpis.ImportKwh, electricity = day.Kpis.ElectricityPence, gas = day.Kpis.GasPence, total = day.Kpis.TotalPence });
        var expected = JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "day-snapshot.json")))!;
        var actual = JsonSerializer.Deserialize<Dictionary<string, decimal>>(snapshot)!;
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order()); foreach (var (key, value) in expected) Assert.Equal(value, actual[key]);
    }
    [Fact]
    public async Task InvalidSettingsReturnFieldErrors()
    {
        using var client = factory.AuthorizedClient(); var response = await client.PutAsJsonAsync("/api/settings", new DashboardSettings { PeakLimitKwh = -1 }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(json.GetProperty("errors").TryGetProperty("peakLimitKwh", out _));
    }
    [Fact]
    public async Task CsvRetainsUtcIdentityAndNoSecrets()
    {
        using var client = factory.AuthorizedClient(); var csv = await client.GetStringAsync("/api/days/2026-10-25/slots.csv"); Assert.Equal(51, csv.Split("\r\n").Length); Assert.Contains("start_utc", csv); Assert.DoesNotContain(ApiFactory.Token, csv);
        var connections = await client.GetStringAsync("/api/connections"); Assert.DoesNotContain("protected", connections, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("refreshToken", connections, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("/api/days/2026-02-30")]
    [InlineData("/api/weeks/2026-10-11")]
    [InlineData("/api/days/2026-10-11/slots?filter=bad")]
    public async Task InvalidQueryIsProblemDetails(string path) { using var client = factory.AuthorizedClient(); var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType); }
    [Theory]
    [InlineData("/api/sync")]
    [InlineData("/api/connections/octopus/test")]
    [InlineData("/api/connections/tesla/test")]
    [InlineData("/api/reports/weekly/send?week=2026-10-05")]
    public async Task SampleModeCannotContactProvidersOrSendReports(string path) { using var client = factory.AuthorizedClient(); Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path, null)).StatusCode); }
    [Fact] public async Task OpenApiContainsEveryReadContract() { using var client = factory.AuthorizedClient(); var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json"); Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("DayDto", out _)); Assert.True(document.GetProperty("paths").TryGetProperty("/api/tesla/sites", out _)); }
    [Fact]
    public async Task TeslaOAuthRequiresInternalAuthAndRejectsSampleMode()
    {
        using var authorized = factory.AuthorizedClient(); using var anonymous = factory.CreateClient();
        var credentials = new TeslaAppCredentials("synthetic-app", "synthetic-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/connections/tesla/authorize", credentials)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await authorized.PostAsJsonAsync("/api/connections/tesla/authorize", credentials)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await authorized.PostAsJsonAsync("/api/connections/tesla/callback", new TeslaAuthorizationCallback(new string('a', 64), "synthetic-code"))).StatusCode);
    }
}

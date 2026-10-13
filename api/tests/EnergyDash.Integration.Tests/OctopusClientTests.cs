using System.Net;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class OctopusClientTests
{
    private sealed class FourRateHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            var body = path.EndsWith("/standard-unit-rates/", StringComparison.Ordinal) ? "{\"count\":0,\"next\":null,\"results\":[]}"
                : path.EndsWith("/IOG-SMB-FIX-12M-26-03-23/", StringComparison.Ordinal) ? """
                {"code":"IOG-SMB-FIX-12M-26-03-23","four_rate_ev_electricity_tariffs":{"_J":{"direct_debit_monthly":{"code":"E-1R-IOG-SMB-FIX-12M-26-03-23-J","links":[{"rel":"day_unit_rates"},{"rel":"night_unit_rates"},{"rel":"ev_device_peak_unit_rates"},{"rel":"ev_device_off_peak_unit_rates"}]}}}}
                """
                : "{\"count\":1,\"next\":null,\"results\":[{\"value_inc_vat\":" + (path.Contains("/night-unit-rates/", StringComparison.Ordinal) ? "8" : "34") + ",\"valid_from\":\"2026-07-05T23:00:00Z\",\"valid_to\":null}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task FourRateSingleRegisterCodeUsesPublishedDayNightRelations()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Octopus:ApiKey"] = "fixture-only-key" }).Build();
        using var handler = new FourRateHandler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.octopus.energy/v1/") };
        var client = new OctopusRestClient(http, new SecretStore(db, new EphemeralDataProtectionProvider(), config));
        var rows = await client.UnitRatesAsync(new() { Fuel = EnergyDash.Core.Fuel.Elec, TariffCode = "E-1R-IOG-SMB-FIX-12M-26-03-23-J", ProductCode = "IOG-SMB-FIX-12M-26-03-23" }, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, default);
        Assert.Equal(2, rows.Count); Assert.Equal(34m, rows.Single(r => r.Register == EnergyDash.Core.RateRegister.Day).Rate.ValueIncVat); Assert.Equal(8m, rows.Single(r => r.Register == EnergyDash.Core.RateRegister.Night).Rate.ValueIncVat);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("/ev-device-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("E-FLAT2R-SILVER-24-12-31-J", "SILVER-24-12-31")]
    [InlineData("E-2R-VAR-22-11-01-J", "VAR-22-11-01")]
    [InlineData("E-1R-IOG-SMB-FIX-12M-26-03-23-J", "IOG-SMB-FIX-12M-26-03-23")]
    [InlineData("G-1R-SILVER-25-09-02-J", "SILVER-25-09-02")]
    public void ProductCodesIncludeFlatDualRegisterAgreements(string tariff, string product) => Assert.Equal(product, OctopusRestClient.ProductCode(tariff));

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public bool ExternalNext { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsoluteUri); Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            var second = request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal);
            var next = ExternalNext ? "https://example.com/steal" : "https://api.octopus.energy/v1/test/?page=2";
            var body = second ? "{\"count\":2,\"next\":null,\"results\":[{\"value_inc_vat\":27.5,\"valid_from\":\"2026-10-11T06:00:00Z\",\"valid_to\":null}]}" : "{\"count\":2,\"next\":\"" + next + "\",\"results\":[{\"value_inc_vat\":7.5,\"valid_from\":\"2026-10-11T00:00:00Z\",\"valid_to\":\"2026-10-11T06:00:00Z\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task ClientFollowsNextAndPreservesOpenEndedRates()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Octopus:ApiKey"] = "fixture-only-key" }).Build(); var secrets = new SecretStore(db, new EphemeralDataProtectionProvider(), config);
        using var handler = new FixtureHandler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.octopus.energy/v1/") };
        var rows = await new OctopusRestClient(http, secrets).PagesAsync<OctopusRate>("test/", default); Assert.Equal(2, rows.Count); Assert.Null(rows[1].ValidTo); Assert.Equal(2, handler.Paths.Count);
    }
    [Fact]
    public async Task PaginationCannotExfiltrateCredentialsToAnotherHost()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Octopus:ApiKey"] = "fixture-only-key" }).Build(); var secrets = new SecretStore(db, new EphemeralDataProtectionProvider(), config);
        using var handler = new FixtureHandler { ExternalNext = true }; using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.octopus.energy/v1/") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OctopusRestClient(http, secrets).PagesAsync<OctopusRate>("test/", default)); Assert.Single(handler.Paths);
    }
}

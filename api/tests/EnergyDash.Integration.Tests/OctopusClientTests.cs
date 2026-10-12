using System.Net;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class OctopusClientTests
{
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

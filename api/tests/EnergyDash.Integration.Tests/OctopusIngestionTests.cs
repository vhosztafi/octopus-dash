using System.Net;
using System.Text;
using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class OctopusIngestionTests
{
    private sealed class SupplierHandler : HttpMessageHandler
    {
        public bool InvalidTariff { get; set; }
        public List<Uri> ConsumptionRequests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = "{\"count\":0,\"next\":null,\"results\":[]}";
            if (request.RequestUri!.AbsolutePath.Contains("/accounts/", StringComparison.Ordinal))
                body = """
                {"properties":[{"electricity_meter_points":[{"mpan":"synthetic-import","is_export":false,"meters":[{"serial_number":"test-electric"}],"agreements":[
                  {"tariff_code":"E-FLAT2R-SILVER-24-12-31-J","valid_from":"2025-04-04T00:00:00Z","valid_to":"2025-05-18T00:00:00Z"},
                  {"tariff_code":"E-FLAT2R-SILVER-24-12-31-J","valid_from":"2025-04-04T00:00:00Z","valid_to":"2025-05-18T00:00:00Z"},
                  {"tariff_code":"E-1R-INTELLI-FIX-12M-26-03-23-J","valid_from":"2026-05-18T00:00:00Z","valid_to":null}]}],
                  "gas_meter_points":[{"mprn":"synthetic-gas","is_export":false,"meters":[{"serial_number":"test-gas"}],"agreements":[
                  {"tariff_code":"G-1R-SILVER-25-09-02-J","valid_from":"2026-04-04T00:00:00Z","valid_to":null}]}]}]}
                """;
            if (InvalidTariff) body = body.Replace("G-1R-SILVER-25-09-02-J", "UNSUPPORTED-TARIFF", StringComparison.Ordinal);
            if (request.RequestUri.AbsolutePath.Contains("/consumption/", StringComparison.Ordinal)) ConsumptionRequests.Add(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [SqlFact]
    public async Task AccountDiscoveryIsAtomicAndNewFuelReceivesItsOwnBackfill()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build(); await sql.StartAsync();
        await using var db = new EnergyDbContext(new DbContextOptionsBuilder<EnergyDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Octopus:ApiKey"] = "synthetic-key", ["Octopus:AccountNumber"] = "A-TEST" }).Build();
        var secrets = new SecretStore(db, new EphemeralDataProtectionProvider(), config);
        using var handler = new SupplierHandler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.octopus.energy/v1/") };
        var settings = new SettingsStore(db);
        var ingest = new IngestionService(db, new OctopusRestClient(http, secrets), null!, null!, settings);
        handler.InvalidTariff = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ingest.AccountAsync(default));
        // A later log save must not accidentally persist changes from failed discovery.
        db.Add(new SyncLog { Source = "OctopusAccount", StartedUtc = DateTime.UtcNow, FinishedUtc = DateTime.UtcNow, Detail = "Failed" }); await db.SaveChangesAsync();
        Assert.Empty(await db.Set<Agreement>().ToListAsync()); Assert.Empty(await db.Set<Meter>().ToListAsync());
        handler.InvalidTariff = false;
        await ingest.AccountAsync(default); await ingest.AccountAsync(default);
        Assert.Equal(2, await db.Set<Meter>().CountAsync()); Assert.Equal(3, await db.Set<Agreement>().CountAsync());
        Assert.Equal("SILVER-24-12-31", (await db.Set<Agreement>().SingleAsync(a => a.TariffCode.StartsWith("E-FLAT2R-"))).ProductCode);
        var now = DateTime.UtcNow;
        db.Add(new Consumption { Fuel = Fuel.Elec, IntervalStartUtc = now.AddHours(-1), IntervalEndUtc = now.AddMinutes(-30), Kwh = 1, RawValue = 1, FetchedUtc = now }); await db.SaveChangesAsync();
        await ingest.ConsumptionAsync(default);
        var today = EnergyTime.LocalDay(now);
        Assert.Contains("period_from=" + Uri.EscapeDataString(EnergyTime.Bounds(today.PlusDays(-7)).Start.ToString("O")), handler.ConsumptionRequests.Single(u => u.AbsolutePath.StartsWith("/v1/electricity-")).Query);
        Assert.Contains("period_from=" + Uri.EscapeDataString(EnergyTime.Bounds(today.PlusDays(-90)).Start.ToString("O")), handler.ConsumptionRequests.Single(u => u.AbsolutePath.StartsWith("/v1/gas-")).Query);
        // High-frequency Tesla logs must not hide the last Octopus account failure.
        db.AddRange(Enumerable.Range(0, 110).Select(i => new SyncLog { Source = "TeslaLive", StartedUtc = now.AddSeconds(i), FinishedUtc = now.AddSeconds(i), Ok = true }));
        db.Add(new SyncLog { Source = "OctopusConsumption", StartedUtc = now, FinishedUtc = now, Ok = true }); await db.SaveChangesAsync();
        Assert.Equal("Sync failed", (await new DashboardQueries(db, settings).StatusAsync(default)).OctopusHealth);
        var start = EnergyTime.Bounds(today.PlusDays(-1)).Start;
        db.Add(new PricedSlot { Fuel = Fuel.Elec, SlotStartUtc = start, TariffCode = "E-1R-IOG-SMB-FIX-12M-26-03-23-J", Kwh = 1, RatePence = 8, CostPence = 8 }); await db.SaveChangesAsync();
        var day = await new DashboardQueries(db, settings).DayAsync(today.PlusDays(-1).ToString("yyyy-MM-dd", null), default);
        Assert.Contains(day.Warnings, w => w.Contains("Smart-charge dispatches have not synced", StringComparison.Ordinal));
    }
}

using System.Text.Json;
using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class ProviderFixtureTests
{
    // Synthetic redacted fixtures matching official docs; replace with real recordings during commissioning.
    [Fact]
    public void OctopusRatesParseOpenEndedVatInclusiveValues()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "octopus-rates.json"));
        var page = JsonSerializer.Deserialize<OctopusPage<OctopusRate>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(page.Results[0].ValidTo); Assert.Equal(7.5m, page.Results[0].ValueIncVat); Assert.NotNull(page.Next);
        Assert.Equal("INTELLI-VAR-22-10-14", OctopusRestClient.ProductCode("E-1R-INTELLI-VAR-22-10-14-C"));
    }
    [Fact]
    public void TeslaWhIsAggregatedIntoHalfHours()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tesla-history.json")));
        var rows = TeslaFleetClient.NormalizeHistory(json.RootElement.GetProperty("response")); Assert.Single(rows); Assert.Equal(.3m, rows[0].GridImportKwh); Assert.Equal(.9m, rows[0].HomeKwh);
    }
    [Fact]
    public void TeslaDailyTotalsAreRejectedRatherThanInventingSlots()
    {
        using var json = JsonDocument.Parse("{\"time_series\":[{\"timestamp\":\"2026-10-11T00:00:00Z\"},{\"timestamp\":\"2026-10-12T00:00:00Z\"}]}"); Assert.Throws<InvalidOperationException>(() => TeslaFleetClient.NormalizeHistory(json.RootElement));
    }
    [Fact]
    public void SqlModelUsesUtcAndDecimalConventions()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);
        foreach (var entity in db.Model.GetEntityTypes()) foreach (var property in entity.GetProperties())
        {
            if (property.Name.EndsWith("Utc", StringComparison.Ordinal)) Assert.Equal("datetime2(0)", property.GetColumnType());
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)) { Assert.Equal(18, property.GetPrecision()); Assert.Equal(6, property.GetScale()); }
        }
    }
}

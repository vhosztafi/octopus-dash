using EnergyDash.Core;
using EnergyDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Testcontainers.MsSql;
using Xunit;

namespace EnergyDash.Integration.Tests;

public sealed class SqlFactAttribute : FactAttribute { public SqlFactAttribute() { if (Environment.GetEnvironmentVariable("RUN_SQL_TESTS") != "1") Skip = "Set RUN_SQL_TESTS=1 with a running Docker Linux daemon to execute real SQL Server checks."; } }
public sealed class SqlServerTests
{
    [SqlFact]
    public async Task MigrationAndRepeatIngestionAreIdempotent()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build(); await sql.StartAsync();
        await using var db = new EnergyDbContext(new DbContextOptionsBuilder<EnergyDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        await db.Database.MigrateAsync(); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var store = new SettingsStore(db); var ingest = new IngestionService(db, null!, null!, null!, store); var start = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 2; i++) await ingest.UpsertConsumptionAsync([new Consumption { Fuel = Fuel.Elec, IntervalStartUtc = start, IntervalEndUtc = start.AddMinutes(30), Kwh = .5m, RawValue = .5m, RawUnit = "kWh", FetchedUtc = start }], default);
        Assert.Equal(1, await db.Set<Consumption>().CountAsync());
        var pricing = new PricingService(db, store); await pricing.RepriceAsync(EnergyTime.ParseDay("2026-10-11"), default); await pricing.RepriceAsync(EnergyTime.ParseDay("2026-10-11"), default);
        Assert.Equal(96, await db.Set<PricedSlot>().CountAsync()); Assert.Equal(2, await db.Set<PricedDay>().CountAsync());

        // Separate providers simulate API and worker processes sharing the persisted key ring.
        var certificatePath = Path.Combine(Path.GetTempPath(), "energy-dash-test-" + Guid.NewGuid().ToString("N") + ".pfx");
        const string certificatePassword = "synthetic-test-certificate-password";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=EnergyDash Integration Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, certificatePassword));
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = sql.GetConnectionString(),
                ["DATA_PROTECTION_CERT"] = certificatePath,
                ["DATA_PROTECTION_CERT_PASSWORD"] = certificatePassword
            }).Build();
            static ServiceProvider Provider(IConfiguration configuration) => new ServiceCollection().AddSingleton(configuration).AddLogging().AddEnergyInfrastructure(configuration).BuildServiceProvider(validateScopes: true);
            using (var apiProvider = Provider(config))
            using (var scope = apiProvider.CreateScope())
                await scope.ServiceProvider.GetRequiredService<SecretStore>().PutAsync("Test__Credential", "synthetic-private-value");
            using (var workerProvider = Provider(config))
            using (var scope = workerProvider.CreateScope())
                Assert.Equal("synthetic-private-value", await scope.ServiceProvider.GetRequiredService<SecretStore>().GetAsync("Test__Credential"));
            db.ChangeTracker.Clear();
            var stored = await db.Set<EnergyDash.Core.Secret>().SingleAsync(s => s.Key == "Test__Credential");
            Assert.DoesNotContain("synthetic-private-value", stored.Protected);
            Assert.Equal("alue", stored.Hint);
            Assert.NotEmpty(await db.DataProtectionKeys.ToListAsync());
        }
        finally { File.Delete(certificatePath); }
    }
}


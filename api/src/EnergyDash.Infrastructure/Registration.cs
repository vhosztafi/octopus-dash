using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Serilog.Core;
using Serilog.Events;

namespace EnergyDash.Infrastructure;

public static class Registration
{
    public static IServiceCollection AddEnergyInfrastructure(this IServiceCollection services, IConfiguration config, bool sampleMode = false)
    {
        services.AddDbContext<EnergyDbContext>(o => o.UseSqlServer(config.GetConnectionString("Default") ?? "Server=localhost;Database=EnergyDash;Integrated Security=true;TrustServerCertificate=true"));
        var dataProtection = services.AddDataProtection().SetApplicationName("EnergyDash");
        if (!sampleMode)
        {
            var certPath = config["DATA_PROTECTION_CERT"];
            if (string.IsNullOrWhiteSpace(certPath)) throw new InvalidOperationException("DATA_PROTECTION_CERT is required outside explicit sample mode.");
            var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, config["DATA_PROTECTION_CERT_PASSWORD"], X509KeyStorageFlags.EphemeralKeySet);
            dataProtection.PersistKeysToDbContext<EnergyDbContext>().ProtectKeysWithCertificate(cert);
        }
        services.AddScoped<SecretStore>(); services.AddScoped<SettingsStore>(); services.AddScoped<IngestionService>(); services.AddScoped<PricingService>(); services.AddScoped<DashboardQueries>(); services.AddScoped<TelemetryService>(); services.AddScoped<AlertService>(); services.AddScoped<ReportService>();
        services.AddHttpClient<OctopusRestClient>(c => c.BaseAddress = new Uri("https://api.octopus.energy/v1/")).AddStandardResilienceHandler();
        services.AddHttpClient<OctopusGraphClient>(c => c.BaseAddress = new Uri("https://api.octopus.energy/v1/graphql/")).AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
        services.AddHttpClient<TeslaFleetClient>().AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
        services.AddHttpClient<TeslaAuthorization>().AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
        services.AddHttpClient<NtfyNotifier>().AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
        services.AddScoped<IBatteryProvider>(sp => sp.GetRequiredService<TeslaFleetClient>());
        services.AddScoped<INotifier, EmailNotifier>(); services.AddScoped<INotifier>(sp => sp.GetRequiredService<NtfyNotifier>());
        return services;
    }
}

public sealed class SecretRedactionPolicy : IDestructuringPolicy
{
    public bool TryDestructure(object value, ILogEventPropertyValueFactory factory, out LogEventPropertyValue result)
    {
        var properties = value.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0).ToArray();
        if (properties.Any(p => Sensitive(p.Name)))
        {
            result = new StructureValue(properties.Select(p => new LogEventProperty(p.Name, Sensitive(p.Name) ? new ScalarValue("[REDACTED]") : factory.CreatePropertyValue(p.GetValue(value), true)))); return true;
        }
        result = new ScalarValue(null); return false;
    }
    private static bool Sensitive(string name) => new[] { "token", "password", "secret", "apikey", "authorization" }.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
}

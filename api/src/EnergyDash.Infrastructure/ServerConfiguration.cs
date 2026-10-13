using Microsoft.Extensions.Configuration;

namespace EnergyDash.Infrastructure;

public static class ServerConfiguration
{
    public static void AddPrivateServerConfiguration(this IConfigurationBuilder configuration)
    {
        var path = Environment.GetEnvironmentVariable("ENERGYDASH_CONFIG_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            // IIS keeps persistent secrets outside the replaceable, publicly served directory.
            configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
            configuration.AddEnvironmentVariables();
        }
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using EnergyDash.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Secret = EnergyDash.Core.Secret;

namespace EnergyDash.Infrastructure;

public sealed class SecretStore(EnergyDbContext db, IDataProtectionProvider protection, IConfiguration configuration)
{
    private readonly IDataProtector protector = protection.CreateProtector("EnergyDash.Secrets");
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var fromEnv = configuration[key.Replace("__", ":", StringComparison.Ordinal)];
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        return await GetStoredAsync(key, ct);
    }
    public async Task<string?> GetStoredAsync(string key, CancellationToken ct = default)
    {
        var row = await db.Set<Secret>().FindAsync([key], ct);
        return row is null ? null : protector.Unprotect(row.Protected);
    }
    public async Task PutAsync(string key, string value, CancellationToken ct = default)
    {
        var row = await db.Set<Secret>().FindAsync([key], ct);
        if (row is null) { row = new Secret { Key = key }; db.Add(row); }
        row.Protected = protector.Protect(value); row.Hint = value.Length <= 4 ? "saved" : value[^4..]; row.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
    // A changed environment seed overrides DB. An unchanged seed never overwrites a rotated token.
    public async Task<string?> GetRefreshTokenAsync(CancellationToken ct)
    {
        var seed = configuration["Tesla:RefreshToken"];
        var stored = await GetStoredAsync("Tesla__RefreshToken", ct);
        if (string.IsNullOrWhiteSpace(seed)) return stored;
        var fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed)));
        if (await GetStoredAsync("Tesla__RefreshSeed", ct) == fingerprint && stored is not null) return stored;
        await PutAsync("Tesla__RefreshToken", seed, ct); await PutAsync("Tesla__RefreshSeed", fingerprint, ct); return seed;
    }
    public async Task<string?> HintAsync(string key, CancellationToken ct)
    {
        var env = configuration[key.Replace("__", ":", StringComparison.Ordinal)];
        if (!string.IsNullOrWhiteSpace(env)) return env.Length > 4 ? env[^4..] : "saved";
        return (await db.Set<Secret>().FindAsync([key], ct))?.Hint;
    }
}

public sealed class SettingsStore(EnergyDbContext db)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public async Task<DashboardSettings> GetAsync(CancellationToken ct = default)
    {
        var row = await db.Set<Setting>().FindAsync(["dashboard"], ct);
        return row is null ? new() : JsonSerializer.Deserialize<DashboardSettings>(row.ValueJson, Options) ?? new();
    }
    public async Task SaveAsync(DashboardSettings settings, CancellationToken ct = default)
    {
        var row = await db.Set<Setting>().FindAsync(["dashboard"], ct);
        if (row is null) { row = new Setting { Key = "dashboard" }; db.Add(row); }
        row.ValueJson = JsonSerializer.Serialize(settings, Options);
        await db.SaveChangesAsync(ct);
    }
}

using EnergyDash.Core;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EnergyDash.Infrastructure;

public sealed class EnergyDbContext(DbContextOptions<EnergyDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(EnergyDbContext).Assembly);
}

public abstract class EntityConfiguration<T> : IEntityTypeConfiguration<T> where T : class
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(typeof(T).Name);
        ConfigureEntity(builder);
        foreach (var p in builder.Metadata.GetProperties())
        {
            if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)) { p.SetPrecision(18); p.SetScale(6); }
            if (p.Name.EndsWith("Utc", StringComparison.Ordinal))
            {
                p.SetColumnType("datetime2(0)");
                if (p.ClrType == typeof(DateTime)) p.SetValueConverter(new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                else if (p.ClrType == typeof(DateTime?)) p.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null));
            }
            if (p.ClrType == typeof(DateOnly)) p.SetColumnType("date");
            if (p.ClrType == typeof(string))
            {
                if (p.Name.EndsWith("Json", StringComparison.Ordinal) || p.Name is "Protected" or "Detail" or "Error") p.SetColumnType("nvarchar(max)");
                else p.SetMaxLength(p.Name == "Hint" ? 8 : p.Name == "Key" ? 128 : 200);
            }
            if (p.ClrType.IsEnum) { p.SetValueConverter(new EnumToStringConverterFactory().Create(p.ClrType)); p.SetMaxLength(16); }
        }
    }
    protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
    private sealed class EnumToStringConverterFactory
    {
        public ValueConverter Create(Type type) => (ValueConverter)Activator.CreateInstance(typeof(EnumToStringConverter<>).MakeGenericType(type), new object?[] { null })!;
    }
}
public sealed class MeterConfiguration : EntityConfiguration<Meter> { protected override void ConfigureEntity(EntityTypeBuilder<Meter> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.Fuel, x.MpanMprn, x.Serial }).IsUnique(); } }
public sealed class AgreementConfiguration : EntityConfiguration<Agreement> { protected override void ConfigureEntity(EntityTypeBuilder<Agreement> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.Fuel, x.TariffCode, x.ValidFromUtc }).IsUnique(); } }
public sealed class UnitRateConfiguration : EntityConfiguration<UnitRate> { protected override void ConfigureEntity(EntityTypeBuilder<UnitRate> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.Fuel, x.TariffCode, x.Register, x.ValidFromUtc }).IsUnique(); } }
public sealed class StandingChargeConfiguration : EntityConfiguration<StandingCharge> { protected override void ConfigureEntity(EntityTypeBuilder<StandingCharge> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.Fuel, x.TariffCode, x.ValidFromUtc }).IsUnique(); } }
public sealed class ConsumptionConfiguration : EntityConfiguration<Consumption> { protected override void ConfigureEntity(EntityTypeBuilder<Consumption> b) => b.HasKey(x => new { x.Fuel, x.IntervalStartUtc }); }
public sealed class DispatchConfiguration : EntityConfiguration<Dispatch> { protected override void ConfigureEntity(EntityTypeBuilder<Dispatch> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.StartUtc, x.EndUtc, x.Source, x.DeviceId }).IsUnique(); } }
public sealed class BatteryConfiguration : EntityConfiguration<BatteryInterval> { protected override void ConfigureEntity(EntityTypeBuilder<BatteryInterval> b) => b.HasKey(x => x.IntervalStartUtc); }
public sealed class LiveConfiguration : EntityConfiguration<LiveAccumulator> { protected override void ConfigureEntity(EntityTypeBuilder<LiveAccumulator> b) => b.HasKey(x => x.SlotStartUtc); }
public sealed class SlotConfiguration : EntityConfiguration<PricedSlot> { protected override void ConfigureEntity(EntityTypeBuilder<PricedSlot> b) => b.HasKey(x => new { x.Fuel, x.SlotStartUtc }); }
public sealed class DayConfiguration : EntityConfiguration<PricedDay> { protected override void ConfigureEntity(EntityTypeBuilder<PricedDay> b) => b.HasKey(x => new { x.Fuel, x.LocalDate }); }
public sealed class SettingConfiguration : EntityConfiguration<Setting> { protected override void ConfigureEntity(EntityTypeBuilder<Setting> b) => b.HasKey(x => x.Key); }
public sealed class SecretConfiguration : EntityConfiguration<Secret> { protected override void ConfigureEntity(EntityTypeBuilder<Secret> b) => b.HasKey(x => x.Key); }
public sealed class RuleConfiguration : EntityConfiguration<AlertRule> { protected override void ConfigureEntity(EntityTypeBuilder<AlertRule> b) { b.HasKey(x => x.Id); b.HasIndex(x => x.Kind).IsUnique(); } }
public sealed class EventConfiguration : EntityConfiguration<AlertEvent> { protected override void ConfigureEntity(EntityTypeBuilder<AlertEvent> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.RuleId, x.SubjectKey }).IsUnique(); b.HasOne<AlertRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict); } }
public sealed class ReportConfiguration : EntityConfiguration<ReportRun> { protected override void ConfigureEntity(EntityTypeBuilder<ReportRun> b) { b.HasKey(x => x.Id); b.HasIndex(x => x.WeekStartLocal).IsUnique(); } }
public sealed class LogConfiguration : EntityConfiguration<SyncLog> { protected override void ConfigureEntity(EntityTypeBuilder<SyncLog> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.Source, x.StartedUtc }); } }
public sealed class CommandConfiguration : EntityConfiguration<SyncCommand> { protected override void ConfigureEntity(EntityTypeBuilder<SyncCommand> b) { b.HasKey(x => x.Id); b.HasIndex(x => new { x.CompletedUtc, x.ClaimedUtc }); } }

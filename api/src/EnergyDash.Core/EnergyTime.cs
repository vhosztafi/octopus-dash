using NodaTime;
using NodaTime.Text;

namespace EnergyDash.Core;

public static class EnergyTime
{
    public static readonly DateTimeZone London = DateTimeZoneProviders.Tzdb["Europe/London"];
    public static LocalDate ParseDay(string text) => LocalDatePattern.Iso.Parse(text).GetValueOrThrow();
    public static DateTime Utc(Instant instant) => instant.ToDateTimeUtc();
    public static Instant Instant(DateTime utc) => NodaTime.Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    public static LocalDate LocalDay(DateTime utc) => Instant(utc).InZone(London).Date;
    public static DateOnly Date(LocalDate date) => new(date.Year, date.Month, date.Day);
    public static LocalDate Local(DateOnly date) => new(date.Year, date.Month, date.Day);
    public static (DateTime Start, DateTime End) Bounds(LocalDate day) => (Utc(day.AtStartOfDayInZone(London).ToInstant()), Utc(day.PlusDays(1).AtStartOfDayInZone(London).ToInstant()));
    public static IReadOnlyList<DateTime> Slots(LocalDate day)
    {
        var (start, end) = Bounds(day);
        var result = new List<DateTime>();
        for (var time = start; time < end; time = time.AddMinutes(30)) result.Add(time);
        return result;
    }
    public static DateTime SlotStart(DateTime utc) => new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 30 * 30, 0, DateTimeKind.Utc);
    public static LocalDate AllowanceDay(DateTime utc)
    {
        var local = Instant(utc).InZone(London);
        return local.Hour < 12 ? local.Date.PlusDays(-1) : local.Date;
    }
    public static bool Contains(DateTime from, DateTime? to, DateTime slot) => from <= slot && (to is null || slot < to);
}

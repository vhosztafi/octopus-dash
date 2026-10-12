using EnergyDash.Core;
using NodaTime;
using Xunit;

namespace EnergyDash.Core.Tests;

public sealed class PricingTests
{
    private static PricingInput Input(string date = "2026-10-11", Fuel fuel = Fuel.Elec)
    {
        var day = EnergyTime.ParseDay(date); var (start, end) = EnergyTime.Bounds(day);
        var consumption = EnergyTime.Slots(day).Select(s => new ConsumptionInterval(s, s.AddMinutes(30), 1m)).ToArray();
        var rates = EnergyTime.Slots(day).Select(s => new UnitRate { Fuel = fuel, TariffCode = "TEST", ValidFromUtc = s, ValidToUtc = s.AddMinutes(30), PenceIncVat = fuel == Fuel.Gas ? 6m : EnergyTime.Instant(s).InZone(EnergyTime.London).Hour < 6 ? 7.5m : 27.5m }).ToArray();
        return new(day, fuel, consumption, [new Agreement { Fuel = fuel, TariffCode = "TEST", ValidFromUtc = start.AddDays(-2), ValidToUtc = null }], rates, [new StandingCharge { Fuel = fuel, TariffCode = "TEST", ValidFromUtc = start, ValidToUtc = end, PencePerDayIncVat = 50m }], [], new());
    }
    [Theory]
    [InlineData("2026-10-11", 48)]
    [InlineData("2026-10-25", 50)]
    [InlineData("2026-03-29", 46)]
    public void LondonDayHasActualHalfHours(string day, int count)
    {
        var result = new PricingEngine().PriceDay(Input(day)); Assert.Equal(count, result.Slots.Count);
        Assert.All(result.Slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.EndUtc - s.StartUtc)); Assert.Equal(50m, result.StandingPence);
        Assert.Equal(result.Slots.Sum(s => s.Kwh * s.RatePence), result.EnergyCostPence);
    }
    [Fact]
    public void RateWindowsDetermineOffPeakAndFullDecimalCost()
    {
        var input = Input(); var result = new PricingEngine().PriceDay(input);
        Assert.Equal(12, result.Slots.Count(s => s.Band == RateBand.Off)); Assert.Equal(1080m, result.EnergyCostPence);
        var readings = input.Consumption.Select(c => c with { Kwh = .123456m }).ToArray(); var precise = new PricingEngine().PriceDay(input with { Consumption = readings }); Assert.Equal(.92592m, precise.Slots[0].CostPence);
    }
    [Theory]
    [InlineData(5, 10, 1)]
    [InlineData(20, 40, 2)]
    [InlineData(-5, 5, 2)]
    public void AnyDispatchOverlapOverridesWholeSlots(int offset, int endOffset, int expected)
    {
        var input = Input(); var start = input.Consumption[26].StartUtc;
        var result = new PricingEngine().PriceDay(input with { Dispatches = [new(start.AddMinutes(offset), start.AddMinutes(endOffset))] });
        Assert.Equal(expected, result.Slots.Count(s => s.Band == RateBand.Disp)); Assert.All(result.Slots.Where(s => s.Band == RateBand.Disp), s => Assert.Equal(7.5m, s.RatePence));
    }
    [Fact]
    public void DispatchWithinOffPeakStaysOff()
    {
        var input = Input(); var result = new PricingEngine().PriceDay(input with { Dispatches = [new(input.Consumption[0].StartUtc, input.Consumption[1].StartUtc)] }); Assert.Equal(RateBand.Off, result.Slots[0].Band);
    }
    [Fact]
    public void ThirteenthDispatchExceedsMiddayAllowance()
    {
        var input = Input(); var start = input.Consumption[24].StartUtc;
        var result = new PricingEngine().PriceDay(input with { Dispatches = [new(start, start.AddHours(6.5))], Settings = new(DispatchAllowanceEnabled: true) });
        Assert.Equal(12, result.Slots.Count(s => s.Band == RateBand.Disp)); Assert.Equal(27.5m, result.Slots[36].RatePence); Assert.Equal("Dispatch beyond daily allowance", result.Slots[36].RateSource);
    }
    [Fact]
    public void AllowanceCarriesAcrossMidnightAndCanExcludeScheduledCharging()
    {
        var input = Input(); var prior = EnergyTime.Bounds(input.Day.PlusDays(-1)).Start;
        var rows = input.Rates.Concat(EnergyTime.Slots(input.Day.PlusDays(-1)).Select(s => new UnitRate { Fuel = Fuel.Elec, TariffCode = "TEST", ValidFromUtc = s, ValidToUtc = s.AddMinutes(30), PenceIncVat = EnergyTime.Instant(s).InZone(EnergyTime.London).Hour < 6 ? 7.5m : 27.5m })).ToArray();
        var midnight = input.Consumption[0].StartUtc; var morning = input.Consumption[12].StartUtc;
        var result = new PricingEngine().PriceDay(input with { Rates = rows, Dispatches = [new(midnight, morning.AddMinutes(30))], Settings = new(DispatchAllowanceEnabled: true) });
        Assert.Equal("Dispatch beyond daily allowance", result.Slots[12].RateSource);
        result = new PricingEngine().PriceDay(input with { Rates = rows, Dispatches = [new(midnight, morning.AddMinutes(30))], Settings = new(DispatchAllowanceEnabled: true, CountScheduledWindowDispatches: false) }); Assert.Equal(RateBand.Disp, result.Slots[12].Band); _ = prior;
    }
    [Fact]
    public void DisabledOverrideAndBoostStayPeak()
    {
        var input = Input(); var start = input.Consumption[26].StartUtc;
        Assert.Equal(RateBand.Peak, new PricingEngine().PriceDay(input with { Dispatches = [new(start, start.AddMinutes(30))], Settings = new(false) }).Slots[26].Band);
        Assert.Equal(RateBand.Peak, new PricingEngine().PriceDay(input with { Dispatches = [new(start, start.AddMinutes(30), IsBoost: true)] }).Slots[26].Band);
    }
    [Fact]
    public void MissingConsumptionIsNullAndNotZero()
    {
        var input = Input(); var result = new PricingEngine().PriceDay(input with { Consumption = input.Consumption.Skip(1).ToArray() });
        Assert.Null(result.Slots[0].Kwh); Assert.Null(result.Slots[0].CostPence); Assert.Equal(1, result.MissingSlots); Assert.NotEmpty(result.Warnings); Assert.Equal(1072.5m, result.EnergyCostPence);
    }
    [Fact]
    public void MissingRateAndStandingDoNotInventPrices()
    {
        var result = new PricingEngine().PriceDay(Input() with { Rates = [], StandingCharges = [] }); Assert.Equal(48, result.UnpricedSlots); Assert.Null(result.StandingPence); Assert.All(result.Slots, s => Assert.Null(s.CostPence));
    }
    [Fact]
    public void GasConversionAndPricing()
    {
        Assert.Equal(11.220633333333333333333333333m, GasConversion.ToKwh(1m, "m3", 39.5m)); Assert.Equal(2m, GasConversion.ToKwh(2m, "kWh", 39.5m));
        var result = new PricingEngine().PriceDay(Input(fuel: Fuel.Gas)); Assert.All(result.Slots, s => Assert.Equal(RateBand.Std, s.Band)); Assert.Equal(288m, result.EnergyCostPence);
    }
    [Fact]
    public void OpenEndedAndDualRegisterRates()
    {
        var input = Input(); var start = input.Consumption[0].StartUtc;
        var result = new PricingEngine().PriceDay(input with { Rates = [new UnitRate { Fuel = Fuel.Elec, TariffCode = "TEST", Register = RateRegister.Day, ValidFromUtc = start, PenceIncVat = 27.5m }, new UnitRate { Fuel = Fuel.Elec, TariffCode = "TEST", Register = RateRegister.Night, ValidFromUtc = start, PenceIncVat = 7.5m }] });
        Assert.Equal(12, result.Slots.Count(s => s.Band == RateBand.Off)); Assert.Equal(7.5m, result.Slots[47].RatePence); Assert.Equal(27.5m, result.Slots[12].RatePence);
    }
    [Fact]
    public void TariffMidnightAndMiddayRateChanges()
    {
        var input = Input(); var start = input.Consumption[0].StartUtc; var noon = input.Consumption[24].StartUtc;
        var result = new PricingEngine().PriceDay(input with { Agreements = [new Agreement { Fuel = Fuel.Elec, TariffCode = "OLD", ValidFromUtc = start.AddDays(-1), ValidToUtc = start }, new Agreement { Fuel = Fuel.Elec, TariffCode = "TEST", ValidFromUtc = start }], Rates = [new UnitRate { Fuel = Fuel.Elec, TariffCode = "TEST", ValidFromUtc = start, ValidToUtc = noon, PenceIncVat = 10m }, new UnitRate { Fuel = Fuel.Elec, TariffCode = "TEST", ValidFromUtc = noon, PenceIncVat = 20m }] });
        Assert.All(result.Slots, s => Assert.Equal("TEST", s.TariffCode)); Assert.Equal(10m, result.Slots[23].RatePence); Assert.Equal(20m, result.Slots[24].RatePence);
    }
    [Fact]
    public void LiveIntegrationSplitsAtSlotBoundaryAndRejectsGaps()
    {
        var a = new DateTime(2026, 10, 11, 17, 29, 30, DateTimeKind.Utc);
        var rows = LiveIntegration.Between(new(a, 1200m), new(a.AddMinutes(1), 1200m)); Assert.Equal(2, rows.Count); Assert.All(rows, s => Assert.Equal(10m, s.Wh));
        Assert.Empty(LiveIntegration.Between(new(a, 1200m), new(a.AddMinutes(3), 1200m)));
        Assert.Empty(LiveIntegration.Between(new(a, 1200m), new(a, 1200m)));
        Assert.Equal(0m, LiveIntegration.Between(new(a, -1200m), new(a.AddMinutes(1), -1200m)).Sum(x => x.Wh));
    }
    [Fact]
    public void ImportExportSignCrossingDoesNotCancelImport()
    {
        var a = new DateTime(2026, 10, 11, 17, 0, 0, DateTimeKind.Utc);
        Assert.Equal(5m, LiveIntegration.Between(new(a, 1200m), new(a.AddMinutes(1), -1200m)).Sum(x => x.Wh));
    }
}

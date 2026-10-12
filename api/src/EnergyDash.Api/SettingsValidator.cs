using EnergyDash.Core;
using FluentValidation;

namespace EnergyDash.Api;

public sealed class SettingsValidator : AbstractValidator<DashboardSettings>
{
    public SettingsValidator()
    {
        RuleFor(x => x.PeakLimitKwh).InclusiveBetween(.01m, 100m);
        RuleFor(x => x.GasDailyLimitPounds).InclusiveBetween(.01m, 1000m);
        RuleFor(x => x.CalorificValue).InclusiveBetween(30m, 45m);
        RuleFor(x => x.GasUnit).Must(v => v is "m3" or "kWh");
        RuleFor(x => x.DispatchAllowanceSlots).InclusiveBetween(1, 48);
        RuleFor(x => x.NightStart).Matches("^([01][0-9]|2[0-3]):[03]0$"); RuleFor(x => x.NightEnd).Matches("^([01][0-9]|2[0-3]):[03]0$");
        RuleFor(x => x.HistoryDays).InclusiveBetween(1, 730);
        RuleFor(x => x.CooldownMinutes).InclusiveBetween(0, 1440);
        RuleFor(x => x.PeakSource).Must(v => v is "tesla" or "octopus");
        RuleFor(x => x.WeekDay).Must(v => v is "mon" or "tue" or "wed" or "thu" or "fri" or "sat" or "sun");
        RuleFor(x => x.WeekTime).Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$");
        RuleFor(x => x.Recipients).MaximumLength(1000).Must(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(address => System.Net.Mail.MailAddress.TryCreate(address, out _)));
        RuleFor(x => x.Recipients).NotEmpty().When(x => x.WeeklyEnabled || x.NotifyEmail);
        RuleFor(x => x.TeslaSiteId).Matches("^[0-9]+$").When(x => !string.IsNullOrWhiteSpace(x.TeslaSiteId));
        RuleFor(x => x.SmartFlexDeviceId).MaximumLength(200);
    }
}
public sealed record OctopusCredentials(string ApiKey, string AccountNumber);
public sealed record TeslaCredentials(string RefreshToken, string ClientId, string? ClientSecret);

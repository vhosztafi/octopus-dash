using System.Net.Http.Headers;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace EnergyDash.Infrastructure;

public sealed record Notification(string Subject, string Text, string Html, string Recipients, string? Csv = null);
public interface INotifier { string Channel { get; } Task SendAsync(Notification notification, CancellationToken ct); }
public sealed class EmailNotifier(IConfiguration config, SecretStore secrets) : INotifier
{
    public string Channel => "email";
    public async Task SendAsync(Notification notification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config["Smtp:Host"]) || string.IsNullOrWhiteSpace(notification.Recipients)) throw new InvalidOperationException("SMTP host and recipients are required.");
        var message = new MimeMessage(); message.From.Add(MailboxAddress.Parse(config["Smtp:From"] ?? throw new InvalidOperationException("SMTP sender is required.")));
        foreach (var recipient in notification.Recipients.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = notification.Subject; var builder = new BodyBuilder { TextBody = notification.Text, HtmlBody = notification.Html };
        if (notification.Csv is not null) builder.Attachments.Add("half-hours.csv", Encoding.UTF8.GetBytes(notification.Csv), ContentType.Parse("text/csv"));
        message.Body = builder.ToMessageBody(); using var smtp = new SmtpClient();
        var tls = config["Smtp:Tls"] == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(config["Smtp:Host"]!, config.GetValue("Smtp:Port", 587), tls, ct);
        var user = config["Smtp:User"]; if (!string.IsNullOrWhiteSpace(user)) await smtp.AuthenticateAsync(user, await secrets.GetAsync("Smtp__Password", ct) ?? "", ct);
        await smtp.SendAsync(message, ct); await smtp.DisconnectAsync(true, ct);
    }
}
public sealed class NtfyNotifier(HttpClient http, IConfiguration config, SecretStore secrets) : INotifier
{
    public string Channel => "push";
    public async Task SendAsync(Notification notification, CancellationToken ct)
    {
        var topic = config["Ntfy:Topic"]; if (string.IsNullOrWhiteSpace(topic)) throw new InvalidOperationException("ntfy topic is required.");
        var url = (config["Ntfy:BaseUrl"] ?? "https://ntfy.sh").TrimEnd('/') + "/" + Uri.EscapeDataString(topic);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(notification.Text) };
        request.Headers.TryAddWithoutValidation("Title", notification.Subject);
        var token = await secrets.GetAsync("Ntfy__Token", ct); if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
    }
}

using ECommerceBatteryShop.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECommerceBatteryShop.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string recipientEmail, string resetUrl, DateTime expiresAt, CancellationToken ct = default);
}

/// <summary>
/// Owns SMTP/MailKit concerns. Callers pass an already-built reset URL (URL generation is an
/// HTTP concern that stays in the controller); this service composes and sends the message.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _options;

    public EmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, DateTime expiresAt,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new InvalidOperationException("SMTP ayarları eksik. Şifre yenileme e-postası gönderilemedi.");
        }

        var expiresInMinutes = Math.Max(1, (int)Math.Round((expiresAt - DateTime.UtcNow).TotalMinutes));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.SenderName ?? "ECommerce Battery Shop", _options.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Şifre Yenileme Bağlantınız";
        message.Body = new TextPart("plain")
        {
            Text = $"Merhaba,\n\nŞifrenizi yenilemek için aşağıdaki bağlantıya tıklayın:\n{resetUrl}\n\nBağlantı {expiresInMinutes} dakika boyunca geçerlidir.\n\nEğer bu talebi siz oluşturmadıysanız lütfen bu e-postayı yok sayın."
        };

        using var client = new SmtpClient();
        var socketOptions = _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

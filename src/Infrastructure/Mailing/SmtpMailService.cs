using Care.WebApi.Application.Common.Mailing;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Care.WebApi.Infrastructure.Mailing;

public class SmtpMailService : IMailService
{
    private readonly MailSettings _settings;
    private readonly ILogger<SmtpMailService> _logger;

    public SmtpMailService(IOptions<MailSettings> settings, ILogger<SmtpMailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(MailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.DisplayName, _settings.From));

            foreach (string address in request.To)
                email.To.Add(MailboxAddress.Parse(address));

            email.Subject = request.Subject;
            email.Body = new BodyBuilder { HtmlBody = request.Body }.ToMessageBody();

            using var smtp = new SmtpClient();
            var socketOptions = _settings.Port == 465 ? SecureSocketOptions.SslOnConnect
                : _settings.Port == 25 ? SecureSocketOptions.None
                : SecureSocketOptions.StartTlsWhenAvailable;

            await smtp.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_settings.UserName) && !string.IsNullOrEmpty(_settings.Password)
                && smtp.Capabilities.HasFlag(SmtpCapabilities.Authentication))
            {
                await smtp.AuthenticateAsync(_settings.UserName, _settings.Password, cancellationToken);
            }

            await smtp.SendAsync(email, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(",", request.To));
        }
    }
}

using Care.WebApi.Application.Common.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Care.WebApi.Infrastructure.Sms;

public class TwilioSmsService : ISmsService
{
    private readonly SmsSettings _settings;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IOptions<SmsSettings> settings, ILogger<TwilioSmsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AccountSid) || string.IsNullOrEmpty(_settings.AuthToken)
            || string.IsNullOrEmpty(_settings.FromNumber))
        {
            _logger.LogInformation("SMS to {Recipient} skipped — Twilio is not configured.", request.To);
            return;
        }

        try
        {
            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            var options = new CreateMessageOptions(new PhoneNumber(request.To))
            {
                From = new PhoneNumber(_settings.FromNumber),
                Body = request.Body
            };

            await MessageResource.CreateAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {Recipient}", request.To);
        }
    }
}

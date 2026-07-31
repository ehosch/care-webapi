using Care.WebApi.Application.Common.Sms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Sms;

internal static class Startup
{
    internal static IServiceCollection AddSms(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SmsSettings>(config.GetSection(nameof(SmsSettings)));
        services.AddTransient<ISmsService, TwilioSmsService>();

        return services;
    }
}

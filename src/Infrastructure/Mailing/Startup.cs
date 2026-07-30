using Care.WebApi.Application.Common.Mailing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Mailing;

internal static class Startup
{
    internal static IServiceCollection AddMailing(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MailSettings>(config.GetSection(nameof(MailSettings)));
        services.AddTransient<IMailService, SmtpMailService>();

        return services;
    }
}

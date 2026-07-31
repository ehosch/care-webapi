using Care.WebApi.Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Common;

internal static class Startup
{
    internal static IServiceCollection AddAppSettingsService(this IServiceCollection services)
    {
        services.AddScoped<IAppSettingsService, AppSettingsService>();
        return services;
    }
}

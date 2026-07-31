using Care.WebApi.Application.Common.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Notifications;

internal static class Startup
{
    internal static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}

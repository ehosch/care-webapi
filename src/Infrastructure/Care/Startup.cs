using Care.WebApi.Application.Care;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Care;

internal static class Startup
{
    internal static IServiceCollection AddShiftServices(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        return services;
    }
}

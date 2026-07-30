using Care.WebApi.Infrastructure.Auth.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Auth;

internal static class Startup
{
    internal static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SecuritySettings>(config.GetSection(nameof(SecuritySettings)));
        services.AddJwtAuth(config);
        services.AddAuthorization();

        return services;
    }
}

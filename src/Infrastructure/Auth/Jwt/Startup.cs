using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Care.WebApi.Infrastructure.Auth.Jwt;

internal static class Startup
{
    internal static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection($"SecuritySettings:{nameof(JwtSettings)}"));
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services
            .AddAuthentication(options =>
            {
                // AddIdentity() (called before this) registers its own cookie scheme and sets
                // DefaultChallengeScheme to it. The single-string AddAuthentication overload only
                // sets DefaultScheme, which doesn't override that — so both must be set explicitly
                // here, or [Authorize] challenges redirect to a nonexistent /Account/Login instead
                // of returning 401.
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        return services;
    }
}

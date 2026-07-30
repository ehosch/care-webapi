using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Middleware;

internal static class Startup
{
    internal static IServiceCollection AddExceptionMiddleware(this IServiceCollection services) => services;

    internal static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();
}

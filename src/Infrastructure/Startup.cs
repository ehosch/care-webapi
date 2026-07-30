using System.Reflection;
using Asp.Versioning;
using Care.WebApi.Application;
using Care.WebApi.Infrastructure.Auth;
using Care.WebApi.Infrastructure.BackgroundJobs;
using Care.WebApi.Infrastructure.Cors;
using Care.WebApi.Infrastructure.Documents;
using Care.WebApi.Infrastructure.FileStorage;
using Care.WebApi.Infrastructure.Identity;
using Care.WebApi.Infrastructure.Mailing;
using Care.WebApi.Infrastructure.Middleware;
using Care.WebApi.Infrastructure.OpenApi;
using Care.WebApi.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(), // Infrastructure
            typeof(IApplicationMarker).Assembly // Application
        };

        return services
            .AddApiVersioning()
            .AddPersistence(config)
            .AddIdentity()
            .AddAuth(config)
            .AddBackgroundJobs(config)
            .AddCorsPolicy(config)
            .AddMailing(config)
            .AddDocumentStorage(config)
            .AddDocumentServices()
            .AddExceptionMiddleware()
            .AddHealthChecks().Services
            .AddOpenApiDocumentation()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(assemblies))
            .AddValidatorsFromAssemblies(assemblies)
            .AddRouting(options => options.LowercaseUrls = true);
    }

    private static IServiceCollection AddApiVersioning(this IServiceCollection services) =>
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        })
        .Services;

    public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app, IConfiguration config) =>
        app
            .UseExceptionMiddleware()
            .UseRouting()
            .UseCorsPolicy()
            .UseAuthentication()
            .UseAuthorization()
            .UseHangfireDashboardWithAuth(config)
            .UseOpenApiDocumentation();

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapControllers().RequireAuthorization();
        builder.MapHealthChecks("/api/health");
        return builder;
    }
}

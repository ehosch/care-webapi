using Care.WebApi.Application.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.Documents;

internal static class Startup
{
    internal static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        return services;
    }
}

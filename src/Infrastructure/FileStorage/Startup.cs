using Care.WebApi.Application.Common.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.FileStorage;

internal static class Startup
{
    internal static IServiceCollection AddDocumentStorage(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DocumentStorageSettings>(config.GetSection(nameof(DocumentStorageSettings)));
        services.AddSingleton<IDocumentStorageService, LocalDocumentStorageService>();

        return services;
    }
}

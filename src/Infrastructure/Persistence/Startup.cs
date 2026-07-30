using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Care.WebApi.Infrastructure.Persistence;

internal static class Startup
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var databaseSettings = config.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                databaseSettings.ConnectionString,
                ServerVersion.AutoDetect(databaseSettings.ConnectionString),
                e => e.MigrationsAssembly("Care.WebApi.Migrators.MySQL")
                      .SchemaBehavior(MySqlSchemaBehavior.Ignore)));

        return services;
    }
}

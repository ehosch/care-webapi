using Care.WebApi.Infrastructure.Care;
using Hangfire;
using Hangfire.MySql;
using HangfireBasicAuthenticationFilter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Care.WebApi.Infrastructure.BackgroundJobs;

internal static class Startup
{
    internal static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfireServer(options => config.GetSection("HangfireSettings:Server").Bind(options));

        var storageSettings = config.GetSection("HangfireSettings:Storage").Get<HangfireStorageSettings>()
            ?? throw new InvalidOperationException("HangfireSettings:Storage not configured.");

        services.AddHangfire(hangfireConfig => hangfireConfig
            .UseStorage(new MySqlStorage(
                storageSettings.ConnectionString,
                config.GetSection("HangfireSettings:Storage:Options").Get<MySqlStorageOptions>() ?? new MySqlStorageOptions())));

        return services;
    }

    internal static IApplicationBuilder UseHangfireDashboardWithAuth(this IApplicationBuilder app, IConfiguration config)
    {
        var dashboardOptions = config.GetSection("HangfireSettings:Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();

        dashboardOptions.Authorization = new[]
        {
            new HangfireCustomBasicAuthenticationFilter
            {
                User = config["HangfireSettings:Credentials:User"],
                Pass = config["HangfireSettings:Credentials:Password"]
            }
        };

        return app.UseHangfireDashboard(config["HangfireSettings:Route"], dashboardOptions);
    }

    internal static IApplicationBuilder UseShiftReminderJob(this IApplicationBuilder app)
    {
        RecurringJob.AddOrUpdate<IShiftReminderJob>(
            "shift-reminders",
            job => job.RunAsync(CancellationToken.None),
            "*/10 * * * *");

        return app;
    }
}

namespace Care.WebApi.Host.Configurations;

internal static class Startup
{
    internal static ConfigureHostBuilder AddConfigurations(this ConfigureHostBuilder host)
    {
        host.ConfigureAppConfiguration((context, config) =>
        {
            const string configurationsDirectory = "Configurations";
            var env = context.HostingEnvironment;
            config.AddJsonFile($"{configurationsDirectory}/logger.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/logger.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/cors.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/cors.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/database.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/database.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/hangfire.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/hangfire.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/mail.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/mail.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/security.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/security.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/sms.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"{configurationsDirectory}/sms.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        });
        return host;
    }
}

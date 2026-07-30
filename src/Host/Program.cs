using Care.WebApi.Host.Configurations;
using Care.WebApi.Infrastructure;
using Care.WebApi.Infrastructure.Persistence.Initialization;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
Log.Information("Server booting up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.AddConfigurations();
    builder.Host.UseSerilog((_, config) => config.WriteTo.Console().ReadFrom.Configuration(builder.Configuration));

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync();

    app.UseInfrastructure(builder.Configuration);
    app.MapEndpoints();
    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("StopTheHostException", StringComparison.Ordinal)
    && !ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Server shutting down...");
    Log.CloseAndFlush();
}

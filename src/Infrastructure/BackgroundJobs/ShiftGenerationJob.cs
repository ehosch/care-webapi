using Microsoft.Extensions.Logging;

namespace Care.WebApi.Infrastructure.BackgroundJobs;

public class ShiftGenerationJob
{
    private readonly ILogger<ShiftGenerationJob> _logger;

    public ShiftGenerationJob(ILogger<ShiftGenerationJob> logger)
    {
        _logger = logger;
    }

    public Task RunAsync()
    {
        _logger.LogInformation("ShiftGenerationJob ran (stub — real ShiftTemplate rollover logic lands in Phase 3).");
        return Task.CompletedTask;
    }
}

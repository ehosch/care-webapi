using Care.WebApi.Domain.Care;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Care.WebApi.Infrastructure.BackgroundJobs;

public class ShiftGenerationJob
{
    private const int RollingWeeksAhead = 4;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ShiftGenerationJob> _logger;

    public ShiftGenerationJob(ApplicationDbContext db, ILogger<ShiftGenerationJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var templates = await _db.ShiftTemplates.ToListAsync();
        if (templates.Count == 0)
        {
            _logger.LogWarning("ShiftGenerationJob ran with no ShiftTemplates configured — skipping.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(7 * RollingWeeksAhead);

        var existing = await _db.Shifts
            .Where(s => s.Date >= today && s.Date <= horizon)
            .Select(s => new { s.Date, s.ShiftType })
            .ToListAsync();
        var existingSet = existing.Select(x => (x.Date, x.ShiftType)).ToHashSet();

        var toAdd = new List<Shift>();
        for (var date = today; date <= horizon; date = date.AddDays(1))
        {
            foreach (var template in templates.Where(t => t.DayOfWeek == date.DayOfWeek))
            {
                if (existingSet.Contains((date, template.ShiftType)))
                {
                    continue;
                }

                toAdd.Add(new Shift
                {
                    Date = date,
                    ShiftType = template.ShiftType,
                    StartTime = template.StartTime,
                    EndTime = template.EndTime,
                    Status = ShiftStatus.Open
                });
            }
        }

        if (toAdd.Count > 0)
        {
            _db.Shifts.AddRange(toAdd);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Generated {Count} shifts through {Horizon}.", toAdd.Count, horizon);
        }
        else
        {
            _logger.LogInformation("ShiftGenerationJob ran — no new shifts needed through {Horizon}.", horizon);
        }
    }
}

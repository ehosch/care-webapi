using Care.WebApi.Application.Common.Notifications;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Care.WebApi.Infrastructure.Care;

internal class ShiftReminderJob : IShiftReminderJob
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;

    public ShiftReminderJob(ApplicationDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Server local wall-clock time, matching how Shift.Date/StartTime are treated
        // everywhere else in this codebase as naive local values with no timezone
        // conversion (see ShiftService.GetAbsoluteStart, NotificationTemplates.FormatTime).
        var now = DateTime.Now;
        var cutoff = now.AddHours(1);
        var today = DateOnly.FromDateTime(now);
        var tomorrow = today.AddDays(1);

        // Narrow by Date in SQL first (same pattern as ShiftService.FindOverlappingShiftsAsync),
        // then do the precise absolute-time comparison in memory.
        var candidates = await _db.Shifts
            .Where(s => s.ReminderSentAt == null && s.Date >= today && s.Date <= tomorrow)
            .ToListAsync(cancellationToken);

        foreach (var shift in candidates)
        {
            var start = shift.Date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));
            if (start > now && start <= cutoff)
            {
                await _notificationService.NotifyShiftReminderAsync(shift.AssignedUserId, shift.Date, shift.StartTime, shift.EndTime, cancellationToken);
                shift.ReminderSentAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

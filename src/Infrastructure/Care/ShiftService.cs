using Care.WebApi.Application.Care;
using Care.WebApi.Application.Common.Exceptions;
using Care.WebApi.Domain.Care;
using Care.WebApi.Infrastructure.Identity;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Care.WebApi.Infrastructure.Care;

internal class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShiftService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<ShiftDto>> GetShiftsAsync(DateOnly weekStart, CancellationToken cancellationToken)
    {
        var weekEnd = weekStart.AddDays(6);

        var shifts = await _db.Shifts
            .Where(s => s.Date >= weekStart && s.Date <= weekEnd)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ShiftType)
            .ToListAsync(cancellationToken);

        var names = await ResolveNamesAsync(shifts
            .Where(s => s.AssignedUserId != null)
            .Select(s => s.AssignedUserId!));

        return shifts
            .Select(s => new ShiftDto(
                s.Id,
                s.Date,
                s.ShiftType,
                s.StartTime,
                s.EndTime,
                s.AssignedUserId,
                s.AssignedUserId is null ? null : names.GetValueOrDefault(s.AssignedUserId, "Unknown"),
                s.Status))
            .ToList();
    }

    public async Task AssignShiftAsync(Guid shiftId, string? userId, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (userId is null)
        {
            shift.AssignedUserId = null;
            shift.Status = ShiftStatus.Open;
        }
        else
        {
            if (await _userManager.FindByIdAsync(userId) is null)
            {
                throw new NotFoundException("User not found.");
            }

            shift.AssignedUserId = userId;
            shift.Status = ShiftStatus.Assigned;
        }

        shift.LastModifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<string> userIds)
    {
        var names = new Dictionary<string, string>();
        foreach (string userId in userIds.Distinct())
        {
            if (await _userManager.FindByIdAsync(userId) is { } user)
            {
                names[userId] = user.Name;
            }
        }

        return names;
    }
}

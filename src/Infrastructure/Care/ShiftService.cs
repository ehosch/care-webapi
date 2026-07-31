using Care.WebApi.Application.Care;
using Care.WebApi.Application.Common.Exceptions;
using Care.WebApi.Application.Common.Notifications;
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
    private readonly INotificationService _notificationService;

    public ShiftService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    public async Task<List<ShiftDto>> GetShiftsAsync(DateOnly weekStart, CancellationToken cancellationToken)
    {
        var weekEnd = weekStart.AddDays(6);

        var shifts = await _db.Shifts
            .Where(s => s.Date >= weekStart && s.Date <= weekEnd)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ShiftType)
            .ToListAsync(cancellationToken);

        var shiftIds = shifts.Select(s => s.Id).ToList();
        var pendingRequests = await _db.ReplacementRequests
            .Where(r => shiftIds.Contains(r.ShiftId) && r.Status == ReplacementRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        var pendingByShiftId = pendingRequests.ToDictionary(r => r.ShiftId);

        var noteCounts = await _db.ShiftNotes
            .Where(n => shiftIds.Contains(n.ShiftId))
            .GroupBy(n => n.ShiftId)
            .Select(g => new { ShiftId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ShiftId, x => x.Count, cancellationToken);

        var names = await ResolveNamesAsync(shifts
            .Where(s => s.AssignedUserId != null)
            .Select(s => s.AssignedUserId!));

        var result = new List<ShiftDto>();
        for (int i = 0; i < shifts.Count; i++)
        {
            var s = shifts[i];
            var pending = pendingByShiftId.GetValueOrDefault(s.Id);

            int? gapAfterMinutes = null;
            if (i + 1 < shifts.Count)
            {
                var gap = GetAbsoluteStart(shifts[i + 1]) - GetAbsoluteEnd(s);
                if (gap.TotalMinutes > 0)
                {
                    gapAfterMinutes = (int)gap.TotalMinutes;
                }
            }

            result.Add(new ShiftDto(
                s.Id,
                s.Date,
                s.ShiftType,
                s.StartTime,
                s.EndTime,
                s.AssignedUserId,
                s.AssignedUserId is null ? null : names.GetValueOrDefault(s.AssignedUserId, "Unknown"),
                s.Status,
                pending?.Id,
                pending?.RequestedByUserId,
                noteCounts.GetValueOrDefault(s.Id),
                gapAfterMinutes));
        }

        return result;
    }

    public async Task AssignShiftAsync(Guid shiftId, string? userId, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        await CancelPendingRequestForShiftAsync(shiftId, cancellationToken);

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

        if (userId is not null)
        {
            await _notificationService.NotifyShiftAssignedAsync(userId, shift.Date, shift.ShiftType, cancellationToken);
        }
    }

    public async Task ClaimShiftAsync(Guid shiftId, string userId, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (shift.Status != ShiftStatus.Open)
        {
            throw new ConflictException("This shift is no longer open.");
        }

        shift.AssignedUserId = userId;
        shift.Status = ShiftStatus.Assigned;
        shift.LastModifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyShiftAssignedAsync(userId, shift.Date, shift.ShiftType, cancellationToken);
    }

    public async Task CreateReplacementRequestAsync(Guid shiftId, string requestedByUserId, string? reason, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (shift.AssignedUserId != requestedByUserId)
        {
            throw new ForbiddenException("You can only request a replacement for your own shift.");
        }

        if (shift.Status != ShiftStatus.Assigned)
        {
            throw new ConflictException("This shift is not currently assigned.");
        }

        _db.ReplacementRequests.Add(new ReplacementRequest
        {
            ShiftId = shiftId,
            RequestedByUserId = requestedByUserId,
            Reason = reason,
            Status = ReplacementRequestStatus.Pending
        });

        shift.Status = ShiftStatus.ReplacementRequested;
        shift.LastModifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyReplacementRequestedAsync(shift.Date, shift.ShiftType, requestedByUserId, reason, cancellationToken);
    }

    public async Task CancelReplacementRequestAsync(Guid requestId, string requestingUserId, CancellationToken cancellationToken)
    {
        var request = await _db.ReplacementRequests.FindAsync([requestId], cancellationToken)
            ?? throw new NotFoundException("Replacement request not found.");

        if (request.RequestedByUserId != requestingUserId)
        {
            throw new ForbiddenException("You can only cancel your own replacement request.");
        }

        if (request.Status != ReplacementRequestStatus.Pending)
        {
            throw new ConflictException("This request is no longer pending.");
        }

        request.Status = ReplacementRequestStatus.Cancelled;
        request.LastModifiedOn = DateTime.UtcNow;

        var shift = await _db.Shifts.FindAsync([request.ShiftId], cancellationToken);
        if (shift is not null)
        {
            shift.Status = ShiftStatus.Assigned;
            shift.LastModifiedOn = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClaimReplacementRequestAsync(Guid requestId, string claimingUserId, CancellationToken cancellationToken)
    {
        var request = await _db.ReplacementRequests.FindAsync([requestId], cancellationToken)
            ?? throw new NotFoundException("Replacement request not found.");

        if (request.Status != ReplacementRequestStatus.Pending)
        {
            throw new ConflictException("This request is no longer pending.");
        }

        if (request.RequestedByUserId == claimingUserId)
        {
            throw new ConflictException("You cannot claim your own replacement request.");
        }

        request.Status = ReplacementRequestStatus.Claimed;
        request.ClaimedByUserId = claimingUserId;
        request.LastModifiedOn = DateTime.UtcNow;

        var shift = await _db.Shifts.FindAsync([request.ShiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");
        shift.AssignedUserId = claimingUserId;
        shift.Status = ShiftStatus.Assigned;
        shift.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyReplacementClaimedAsync(request.RequestedByUserId, claimingUserId, shift.Date, shift.ShiftType, cancellationToken);
    }

    public async Task<List<ReplacementRequestDto>> GetReplacementQueueAsync(CancellationToken cancellationToken)
    {
        var requests = await _db.ReplacementRequests
            .Where(r => r.Status == ReplacementRequestStatus.Pending)
            .OrderBy(r => r.CreatedOn)
            .ToListAsync(cancellationToken);

        var shiftIds = requests.Select(r => r.ShiftId).ToList();
        var shifts = await _db.Shifts
            .Where(s => shiftIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var shiftsById = shifts.ToDictionary(s => s.Id);

        var names = await ResolveNamesAsync(requests.Select(r => r.RequestedByUserId));

        return requests
            .Where(r => shiftsById.ContainsKey(r.ShiftId))
            .Select(r =>
            {
                var shift = shiftsById[r.ShiftId];
                return new ReplacementRequestDto(
                    r.Id,
                    r.ShiftId,
                    shift.Date,
                    shift.ShiftType,
                    r.RequestedByUserId,
                    names.GetValueOrDefault(r.RequestedByUserId, "Unknown"),
                    r.Reason,
                    r.CreatedOn);
            })
            .ToList();
    }

    public async Task<List<ShiftNoteDto>> GetShiftNotesAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var notes = await _db.ShiftNotes
            .Where(n => n.ShiftId == shiftId)
            .OrderBy(n => n.CreatedOn)
            .ToListAsync(cancellationToken);

        var names = await ResolveNamesAsync(notes.Select(n => n.AuthorUserId));

        return notes
            .Select(n => new ShiftNoteDto(
                n.Id,
                n.AuthorUserId,
                names.GetValueOrDefault(n.AuthorUserId, "Unknown"),
                n.Text,
                n.CreatedOn))
            .ToList();
    }

    public async Task<ShiftNoteDto> AddShiftNoteAsync(Guid shiftId, string authorUserId, string text, CancellationToken cancellationToken)
    {
        if (await _db.Shifts.FindAsync([shiftId], cancellationToken) is null)
        {
            throw new NotFoundException("Shift not found.");
        }

        var note = new ShiftNote
        {
            ShiftId = shiftId,
            AuthorUserId = authorUserId,
            Text = text
        };

        _db.ShiftNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);

        var names = await ResolveNamesAsync([authorUserId]);
        return new ShiftNoteDto(note.Id, authorUserId, names.GetValueOrDefault(authorUserId, "Unknown"), text, note.CreatedOn);
    }

    private async Task CancelPendingRequestForShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var pending = await _db.ReplacementRequests
            .FirstOrDefaultAsync(r => r.ShiftId == shiftId && r.Status == ReplacementRequestStatus.Pending, cancellationToken);

        if (pending is not null)
        {
            pending.Status = ReplacementRequestStatus.Cancelled;
            pending.LastModifiedOn = DateTime.UtcNow;
        }
    }

    public async Task<ShiftDto> AdjustShiftTimesAsync(Guid shiftId, TimeSpan startTime, TimeSpan endTime, bool confirmGap, string requestingUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (!isAdmin && shift.AssignedUserId != requestingUserId)
        {
            throw new ForbiddenException("You can only adjust the times of your own shift.");
        }

        if (startTime == endTime)
        {
            throw new ConflictException("Start and end time cannot be the same.");
        }

        var precedingShift = await _db.Shifts
            .Where(s => s.Date < shift.Date || (s.Date == shift.Date && s.ShiftType < shift.ShiftType))
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.ShiftType)
            .FirstOrDefaultAsync(cancellationToken);

        var followingShift = await _db.Shifts
            .Where(s => s.Date > shift.Date || (s.Date == shift.Date && s.ShiftType > shift.ShiftType))
            .OrderBy(s => s.Date).ThenBy(s => s.ShiftType)
            .FirstOrDefaultAsync(cancellationToken);

        var newAbsoluteStart = shift.Date.ToDateTime(TimeOnly.FromTimeSpan(startTime));
        var newAbsoluteEnd = shift.Date.ToDateTime(TimeOnly.FromTimeSpan(endTime));
        if (newAbsoluteEnd <= newAbsoluteStart)
        {
            newAbsoluteEnd = newAbsoluteEnd.AddDays(1);
        }

        var conflictMessages = new List<string>();

        if (precedingShift is not null)
        {
            var gap = newAbsoluteStart - GetAbsoluteEnd(precedingShift);
            if (gap.TotalMinutes > 0)
            {
                if (precedingShift.Status == ShiftStatus.Open)
                {
                    precedingShift.EndTime = newAbsoluteStart.TimeOfDay;
                    precedingShift.LastModifiedOn = DateTime.UtcNow;
                }
                else if (!confirmGap)
                {
                    conflictMessages.Add($"This will leave a {(int)gap.TotalMinutes}-minute gap before this shift, after the {precedingShift.ShiftType} shift on {precedingShift.Date:ddd, MMM d}.");
                }
            }
        }

        if (followingShift is not null)
        {
            var gap = GetAbsoluteStart(followingShift) - newAbsoluteEnd;
            if (gap.TotalMinutes > 0)
            {
                if (followingShift.Status == ShiftStatus.Open)
                {
                    followingShift.StartTime = newAbsoluteEnd.TimeOfDay;
                    followingShift.LastModifiedOn = DateTime.UtcNow;
                }
                else if (!confirmGap)
                {
                    conflictMessages.Add($"This will leave a {(int)gap.TotalMinutes}-minute gap after this shift, before the {followingShift.ShiftType} shift on {followingShift.Date:ddd, MMM d}.");
                }
            }
        }

        if (conflictMessages.Count > 0)
        {
            throw new ConflictException(string.Join(" ", conflictMessages) + " Continue anyway?");
        }

        shift.StartTime = startTime;
        shift.EndTime = endTime;
        shift.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (confirmGap)
        {
            if (precedingShift is { Status: not ShiftStatus.Open, AssignedUserId: { } precedingUserId })
            {
                var gap = newAbsoluteStart - GetAbsoluteEnd(precedingShift);
                if (gap.TotalMinutes > 0)
                {
                    await _notificationService.NotifyScheduleGapAsync(precedingUserId, precedingShift.Date, precedingShift.ShiftType, (int)gap.TotalMinutes, cancellationToken);
                }
            }

            if (followingShift is { Status: not ShiftStatus.Open, AssignedUserId: { } followingUserId })
            {
                var gap = GetAbsoluteStart(followingShift) - newAbsoluteEnd;
                if (gap.TotalMinutes > 0)
                {
                    await _notificationService.NotifyScheduleGapAsync(followingUserId, followingShift.Date, followingShift.ShiftType, (int)gap.TotalMinutes, cancellationToken);
                }
            }
        }

        int? gapAfterMinutesResult = null;
        if (followingShift is not null)
        {
            var followingGap = GetAbsoluteStart(followingShift) - newAbsoluteEnd;
            if (followingGap.TotalMinutes > 0)
            {
                gapAfterMinutesResult = (int)followingGap.TotalMinutes;
            }
        }

        var names = await ResolveNamesAsync(shift.AssignedUserId is null ? Array.Empty<string>() : new[] { shift.AssignedUserId });
        var pending = await _db.ReplacementRequests
            .FirstOrDefaultAsync(r => r.ShiftId == shiftId && r.Status == ReplacementRequestStatus.Pending, cancellationToken);
        int noteCount = await _db.ShiftNotes.CountAsync(n => n.ShiftId == shiftId, cancellationToken);

        return new ShiftDto(
            shift.Id,
            shift.Date,
            shift.ShiftType,
            shift.StartTime,
            shift.EndTime,
            shift.AssignedUserId,
            shift.AssignedUserId is null ? null : names.GetValueOrDefault(shift.AssignedUserId, "Unknown"),
            shift.Status,
            pending?.Id,
            pending?.RequestedByUserId,
            noteCount,
            gapAfterMinutesResult);
    }

    private static DateTime GetAbsoluteStart(Shift shift) =>
        shift.Date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));

    private static DateTime GetAbsoluteEnd(Shift shift)
    {
        var start = GetAbsoluteStart(shift);
        var end = shift.Date.ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime));
        return end <= start ? end.AddDays(1) : end;
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

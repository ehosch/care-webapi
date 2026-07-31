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
    private static readonly TimeSpan MaxShiftDuration = TimeSpan.FromHours(24);

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
            .ThenBy(s => s.StartTime)
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

        var names = await ResolveNamesAsync(shifts.Select(s => s.AssignedUserId));

        var result = new List<ShiftDto>();
        foreach (var s in shifts)
        {
            var pending = pendingByShiftId.GetValueOrDefault(s.Id);

            result.Add(new ShiftDto(
                s.Id,
                s.Date,
                s.StartTime,
                s.EndTime,
                s.AssignedUserId,
                names.GetValueOrDefault(s.AssignedUserId, "Unknown"),
                s.Status,
                pending?.Id,
                pending?.RequestedByUserId,
                noteCounts.GetValueOrDefault(s.Id)));
        }

        return result;
    }

    public async Task<ShiftDto> CreateShiftAsync(DateOnly date, TimeSpan startTime, TimeSpan endTime, string assignedUserId, string requestingUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (startTime == endTime)
        {
            throw new ConflictException("Start and end time cannot be the same.");
        }

        if (!isAdmin && assignedUserId != requestingUserId)
        {
            throw new ForbiddenException("You can only claim shifts for yourself.");
        }

        if (await _userManager.FindByIdAsync(assignedUserId) is null)
        {
            throw new NotFoundException("User not found.");
        }

        var newAbsoluteStart = date.ToDateTime(TimeOnly.FromTimeSpan(startTime));
        var newAbsoluteEnd = date.ToDateTime(TimeOnly.FromTimeSpan(endTime));
        if (newAbsoluteEnd <= newAbsoluteStart)
        {
            newAbsoluteEnd = newAbsoluteEnd.AddDays(1);
        }

        if (newAbsoluteEnd - newAbsoluteStart > MaxShiftDuration)
        {
            throw new ConflictException("A single shift can't be longer than 24 hours.");
        }

        var pendingNotifications = await ResolveOverlapsAsync(newAbsoluteStart, newAbsoluteEnd, excludeShiftId: null, cancellationToken);

        var shift = new Shift
        {
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            AssignedUserId = assignedUserId,
            Status = ShiftStatus.Assigned
        };
        _db.Shifts.Add(shift);

        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyShiftAssignedAsync(assignedUserId, date, startTime, endTime, cancellationToken);
        await FirePendingNotificationsAsync(pendingNotifications, cancellationToken);

        return await MapToDtoAsync(shift, cancellationToken);
    }

    public async Task AssignShiftAsync(Guid shiftId, string userId, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (await _userManager.FindByIdAsync(userId) is null)
        {
            throw new NotFoundException("User not found.");
        }

        await CancelPendingRequestForShiftAsync(shiftId, cancellationToken);

        shift.AssignedUserId = userId;
        shift.Status = ShiftStatus.Assigned;
        shift.LastModifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyShiftAssignedAsync(userId, shift.Date, shift.StartTime, shift.EndTime, cancellationToken);
    }

    public async Task DeleteShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts.FindAsync([shiftId], cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (await _db.ReplacementRequests.AnyAsync(r => r.ShiftId == shiftId && r.Status == ReplacementRequestStatus.Pending, cancellationToken))
        {
            throw new ConflictException("This shift has a pending replacement request and can't be removed right now.");
        }

        var notes = await _db.ShiftNotes.Where(n => n.ShiftId == shiftId).ToListAsync(cancellationToken);
        _db.ShiftNotes.RemoveRange(notes);

        string assignedUserId = shift.AssignedUserId;
        DateOnly date = shift.Date;
        TimeSpan startTime = shift.StartTime;
        TimeSpan endTime = shift.EndTime;

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyShiftRemovedAsync(assignedUserId, date, startTime, endTime, cancellationToken);
    }

    public async Task<ShiftDto> AdjustShiftTimesAsync(Guid shiftId, DateOnly date, TimeSpan startTime, TimeSpan endTime, string requestingUserId, bool isAdmin, CancellationToken cancellationToken)
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

        var oldAbsoluteStart = GetAbsoluteStart(shift);
        var oldAbsoluteEnd = GetAbsoluteEnd(shift);

        var newAbsoluteStart = date.ToDateTime(TimeOnly.FromTimeSpan(startTime));
        var newAbsoluteEnd = date.ToDateTime(TimeOnly.FromTimeSpan(endTime));
        if (newAbsoluteEnd <= newAbsoluteStart)
        {
            newAbsoluteEnd = newAbsoluteEnd.AddDays(1);
        }

        if (newAbsoluteEnd - newAbsoluteStart > MaxShiftDuration)
        {
            throw new ConflictException("A single shift can't be longer than 24 hours.");
        }

        var pendingNotifications = new List<PendingShiftNotification>();

        if (newAbsoluteStart < oldAbsoluteStart)
        {
            pendingNotifications.AddRange(await ResolveOverlapsAsync(newAbsoluteStart, oldAbsoluteStart, shiftId, cancellationToken));
        }
        else if (newAbsoluteStart > oldAbsoluteStart)
        {
            var neighbor = await FindShiftEndingExactlyAtAsync(oldAbsoluteStart, shiftId, cancellationToken);
            if (neighbor is not null)
            {
                SetAbsoluteEnd(neighbor, newAbsoluteStart);
                pendingNotifications.Add(new PendingShiftNotification(neighbor.AssignedUserId, neighbor.Date, neighbor.StartTime, neighbor.EndTime, WasRemoved: false));
            }
        }

        if (newAbsoluteEnd > oldAbsoluteEnd)
        {
            pendingNotifications.AddRange(await ResolveOverlapsAsync(oldAbsoluteEnd, newAbsoluteEnd, shiftId, cancellationToken));
        }
        else if (newAbsoluteEnd < oldAbsoluteEnd)
        {
            var neighbor = await FindShiftStartingExactlyAtAsync(oldAbsoluteEnd, shiftId, cancellationToken);
            if (neighbor is not null)
            {
                SetAbsoluteStart(neighbor, newAbsoluteEnd);
                pendingNotifications.Add(new PendingShiftNotification(neighbor.AssignedUserId, neighbor.Date, neighbor.StartTime, neighbor.EndTime, WasRemoved: false));
            }
        }

        shift.Date = DateOnly.FromDateTime(newAbsoluteStart);
        shift.StartTime = newAbsoluteStart.TimeOfDay;
        shift.EndTime = newAbsoluteEnd.TimeOfDay;
        shift.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await FirePendingNotificationsAsync(pendingNotifications, cancellationToken);

        return await MapToDtoAsync(shift, cancellationToken);
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

        await _notificationService.NotifyReplacementRequestedAsync(shift.Date, shift.StartTime, shift.EndTime, requestedByUserId, reason, cancellationToken);
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

        await _notificationService.NotifyReplacementClaimedAsync(request.RequestedByUserId, claimingUserId, shift.Date, shift.StartTime, shift.EndTime, cancellationToken);
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

    /// <summary>
    /// For each existing shift overlapping [rangeStart, rangeEnd) (excluding <paramref name="excludeShiftId"/>),
    /// either shrinks its stuck-out edge to the new boundary, or — if fully contained within the new range —
    /// deletes it (blocked if it has a pending replacement request). A shift sticking out on both sides would
    /// require splitting it in two, which isn't supported; that's rejected outright since the designed UI can
    /// never construct such a range (only boundary-adjacent cells are ever clickable).
    /// </summary>
    private async Task<List<PendingShiftNotification>> ResolveOverlapsAsync(DateTime rangeStart, DateTime rangeEnd, Guid? excludeShiftId, CancellationToken cancellationToken)
    {
        var notifications = new List<PendingShiftNotification>();
        var overlapping = await FindOverlappingShiftsAsync(rangeStart, rangeEnd, excludeShiftId, cancellationToken);

        foreach (var existing in overlapping)
        {
            var existingStart = GetAbsoluteStart(existing);
            var existingEnd = GetAbsoluteEnd(existing);
            bool sticksOutBefore = existingStart < rangeStart;
            bool sticksOutAfter = existingEnd > rangeEnd;

            if (sticksOutBefore && sticksOutAfter)
            {
                throw new ConflictException($"This would split the existing shift on {existing.Date:ddd, MMM d} in the middle, which isn't supported — adjust it from one of its edges instead.");
            }

            if (!sticksOutBefore && !sticksOutAfter)
            {
                if (await _db.ReplacementRequests.AnyAsync(r => r.ShiftId == existing.Id && r.Status == ReplacementRequestStatus.Pending, cancellationToken))
                {
                    throw new ConflictException($"The shift on {existing.Date:ddd, MMM d} has a pending replacement request and can't be removed right now.");
                }

                var notes = await _db.ShiftNotes.Where(n => n.ShiftId == existing.Id).ToListAsync(cancellationToken);
                _db.ShiftNotes.RemoveRange(notes);

                notifications.Add(new PendingShiftNotification(existing.AssignedUserId, existing.Date, existing.StartTime, existing.EndTime, WasRemoved: true));
                _db.Shifts.Remove(existing);
            }
            else if (sticksOutBefore)
            {
                SetAbsoluteEnd(existing, rangeStart);
                notifications.Add(new PendingShiftNotification(existing.AssignedUserId, existing.Date, existing.StartTime, existing.EndTime, WasRemoved: false));
            }
            else
            {
                SetAbsoluteStart(existing, rangeEnd);
                notifications.Add(new PendingShiftNotification(existing.AssignedUserId, existing.Date, existing.StartTime, existing.EndTime, WasRemoved: false));
            }
        }

        return notifications;
    }

    private async Task FirePendingNotificationsAsync(List<PendingShiftNotification> notifications, CancellationToken cancellationToken)
    {
        foreach (var n in notifications)
        {
            if (n.WasRemoved)
            {
                await _notificationService.NotifyShiftRemovedAsync(n.UserId, n.Date, n.StartTime, n.EndTime, cancellationToken);
            }
            else
            {
                await _notificationService.NotifyShiftBoundaryChangedAsync(n.UserId, n.Date, n.StartTime, n.EndTime, cancellationToken);
            }
        }
    }

    private async Task<List<Shift>> FindOverlappingShiftsAsync(DateTime rangeStart, DateTime rangeEnd, Guid? excludeShiftId, CancellationToken cancellationToken)
    {
        var lo = DateOnly.FromDateTime(rangeStart.AddDays(-2));
        var hi = DateOnly.FromDateTime(rangeEnd.AddDays(1));

        var candidates = await _db.Shifts
            .Where(s => s.Date >= lo && s.Date <= hi && (excludeShiftId == null || s.Id != excludeShiftId.Value))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(s => GetAbsoluteStart(s) < rangeEnd && GetAbsoluteEnd(s) > rangeStart)
            .OrderBy(s => GetAbsoluteStart(s))
            .ToList();
    }

    private async Task<Shift?> FindShiftEndingExactlyAtAsync(DateTime instant, Guid excludeShiftId, CancellationToken cancellationToken)
    {
        var candidates = await FindOverlappingShiftsAsync(instant.AddMinutes(-1), instant, excludeShiftId, cancellationToken);
        return candidates.FirstOrDefault(s => GetAbsoluteEnd(s) == instant);
    }

    private async Task<Shift?> FindShiftStartingExactlyAtAsync(DateTime instant, Guid excludeShiftId, CancellationToken cancellationToken)
    {
        var candidates = await FindOverlappingShiftsAsync(instant, instant.AddMinutes(1), excludeShiftId, cancellationToken);
        return candidates.FirstOrDefault(s => GetAbsoluteStart(s) == instant);
    }

    private static void SetAbsoluteStart(Shift shift, DateTime newStart)
    {
        var end = GetAbsoluteEnd(shift);
        shift.Date = DateOnly.FromDateTime(newStart);
        shift.StartTime = newStart.TimeOfDay;
        shift.EndTime = end.TimeOfDay;
        shift.LastModifiedOn = DateTime.UtcNow;
    }

    private static void SetAbsoluteEnd(Shift shift, DateTime newEnd)
    {
        shift.EndTime = newEnd.TimeOfDay;
        shift.LastModifiedOn = DateTime.UtcNow;
    }

    private static DateTime GetAbsoluteStart(Shift shift) =>
        shift.Date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));

    private static DateTime GetAbsoluteEnd(Shift shift)
    {
        var start = GetAbsoluteStart(shift);
        var end = shift.Date.ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime));
        return end <= start ? end.AddDays(1) : end;
    }

    private async Task<ShiftDto> MapToDtoAsync(Shift shift, CancellationToken cancellationToken)
    {
        var names = await ResolveNamesAsync([shift.AssignedUserId]);
        var pending = await _db.ReplacementRequests
            .FirstOrDefaultAsync(r => r.ShiftId == shift.Id && r.Status == ReplacementRequestStatus.Pending, cancellationToken);
        int noteCount = await _db.ShiftNotes.CountAsync(n => n.ShiftId == shift.Id, cancellationToken);

        return new ShiftDto(
            shift.Id,
            shift.Date,
            shift.StartTime,
            shift.EndTime,
            shift.AssignedUserId,
            names.GetValueOrDefault(shift.AssignedUserId, "Unknown"),
            shift.Status,
            pending?.Id,
            pending?.RequestedByUserId,
            noteCount);
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

    private sealed record PendingShiftNotification(string UserId, DateOnly Date, TimeSpan StartTime, TimeSpan EndTime, bool WasRemoved);
}

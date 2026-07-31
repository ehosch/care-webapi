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

        return shifts
            .Select(s =>
            {
                var pending = pendingByShiftId.GetValueOrDefault(s.Id);
                return new ShiftDto(
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
                    noteCounts.GetValueOrDefault(s.Id));
            })
            .ToList();
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

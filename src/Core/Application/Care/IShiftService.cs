namespace Care.WebApi.Application.Care;

public interface IShiftService
{
    Task<List<ShiftDto>> GetShiftsAsync(DateOnly weekStart, CancellationToken cancellationToken);

    Task AssignShiftAsync(Guid shiftId, string? userId, CancellationToken cancellationToken);

    Task ClaimShiftAsync(Guid shiftId, string userId, CancellationToken cancellationToken);

    Task CreateReplacementRequestAsync(Guid shiftId, string requestedByUserId, string? reason, CancellationToken cancellationToken);

    Task CancelReplacementRequestAsync(Guid requestId, string requestingUserId, CancellationToken cancellationToken);

    Task ClaimReplacementRequestAsync(Guid requestId, string claimingUserId, CancellationToken cancellationToken);

    Task<List<ReplacementRequestDto>> GetReplacementQueueAsync(CancellationToken cancellationToken);

    Task<List<ShiftNoteDto>> GetShiftNotesAsync(Guid shiftId, CancellationToken cancellationToken);

    Task<ShiftNoteDto> AddShiftNoteAsync(Guid shiftId, string authorUserId, string text, CancellationToken cancellationToken);
}

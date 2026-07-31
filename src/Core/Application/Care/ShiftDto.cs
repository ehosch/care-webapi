using Care.WebApi.Domain.Care;

namespace Care.WebApi.Application.Care;

public record ShiftDto(
    Guid Id,
    DateOnly Date,
    ShiftType ShiftType,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? AssignedUserId,
    string? AssignedUserName,
    ShiftStatus Status,
    Guid? PendingReplacementRequestId,
    string? PendingReplacementRequestedByUserId,
    int NoteCount,
    int? GapAfterMinutes);

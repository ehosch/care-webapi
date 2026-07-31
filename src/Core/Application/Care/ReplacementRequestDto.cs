namespace Care.WebApi.Application.Care;

public record ReplacementRequestDto(
    Guid Id,
    Guid ShiftId,
    DateOnly ShiftDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string RequestedByUserId,
    string RequestedByName,
    string? Reason,
    DateTime CreatedAt);

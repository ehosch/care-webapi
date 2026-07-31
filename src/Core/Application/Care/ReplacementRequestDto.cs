namespace Care.WebApi.Application.Care;

public record ReplacementRequestDto(
    Guid Id,
    Guid ShiftId,
    DateOnly ShiftDate,
    string RequestedByUserId,
    string RequestedByName,
    string? Reason,
    DateTime CreatedAt);

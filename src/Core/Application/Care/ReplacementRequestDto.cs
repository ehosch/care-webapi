using Care.WebApi.Domain.Care;

namespace Care.WebApi.Application.Care;

public record ReplacementRequestDto(
    Guid Id,
    Guid ShiftId,
    DateOnly ShiftDate,
    ShiftType ShiftType,
    string RequestedByUserId,
    string RequestedByName,
    string? Reason,
    DateTime CreatedAt);

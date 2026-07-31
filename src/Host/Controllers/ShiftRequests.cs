using System.ComponentModel.DataAnnotations;

namespace Care.WebApi.Host.Controllers;

public record CreateShiftRequest(DateOnly Date, TimeSpan StartTime, TimeSpan EndTime, string? AssignedUserId);

public record AssignShiftRequest([Required] string UserId);

public record CreateReplacementRequestRequest(string? Reason);

public record AddShiftNoteRequest([Required][MaxLength(4000)] string Text);

public record AdjustShiftTimesRequest(DateOnly Date, TimeSpan StartTime, TimeSpan EndTime);

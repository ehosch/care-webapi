using System.ComponentModel.DataAnnotations;

namespace Care.WebApi.Host.Controllers;

public record AssignShiftRequest(string? UserId);

public record CreateReplacementRequestRequest(string? Reason);

public record AddShiftNoteRequest([Required][MaxLength(4000)] string Text);

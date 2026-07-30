namespace Care.WebApi.Application.Care;

public record ShiftNoteDto(
    Guid Id,
    string AuthorUserId,
    string AuthorName,
    string Text,
    DateTime CreatedAt);

namespace Care.WebApi.Application.Identity.Users;

public record UserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTime InvitedAt,
    DateTime? JoinedAt);

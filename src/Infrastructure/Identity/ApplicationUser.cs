using Microsoft.AspNetCore.Identity;

namespace Care.WebApi.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = default!;
    public UserStatus Status { get; set; } = UserStatus.Invited;
    public DateTime InvitedAt { get; set; }
    public DateTime? JoinedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
}

public enum UserStatus
{
    Invited,
    Active
}

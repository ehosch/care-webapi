using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Identity;

public class Invite : AuditableEntity
{
    public string Email { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string CreatedByUserId { get; set; } = default!;
    public DateTime? UsedAt { get; set; }
}

using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Care;

public class ShiftNote : AuditableEntity
{
    public Guid ShiftId { get; set; }
    public string AuthorUserId { get; set; } = default!;
    public string Text { get; set; } = default!;
}

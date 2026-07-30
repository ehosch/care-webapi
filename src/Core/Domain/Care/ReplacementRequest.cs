using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Care;

public class ReplacementRequest : AuditableEntity
{
    public Guid ShiftId { get; set; }
    public string RequestedByUserId { get; set; } = default!;
    public string? Reason { get; set; }
    public ReplacementRequestStatus Status { get; set; } = ReplacementRequestStatus.Pending;
    public string? ClaimedByUserId { get; set; }
}

using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Care;

public class Shift : AuditableEntity
{
    public DateOnly Date { get; set; }
    public ShiftType ShiftType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? AssignedUserId { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
}

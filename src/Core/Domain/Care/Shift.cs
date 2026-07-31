using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Care;

public class Shift : AuditableEntity
{
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string AssignedUserId { get; set; } = string.Empty;
    public ShiftStatus Status { get; set; } = ShiftStatus.Assigned;
    public DateTime? ReminderSentAt { get; set; }
}

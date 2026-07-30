using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Care;

public class ShiftTemplate : AuditableEntity
{
    public DayOfWeek DayOfWeek { get; set; }
    public ShiftType ShiftType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

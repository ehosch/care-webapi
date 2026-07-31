using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Common;

public class AppSettings : AuditableEntity
{
    public string? PatientName { get; set; }

    public bool NotifyShiftAssignedEmail { get; set; } = true;
    public bool NotifyShiftAssignedSms { get; set; } = true;
    public bool NotifyReplacementRequestedEmail { get; set; } = true;
    public bool NotifyReplacementRequestedSms { get; set; } = true;
    public bool NotifyReplacementClaimedEmail { get; set; } = true;
    public bool NotifyReplacementClaimedSms { get; set; } = true;
    public bool NotifyDocumentUploadedEmail { get; set; } = true;
    public bool NotifyDocumentUploadedSms { get; set; } = true;
    public bool NotifyShiftRemovedEmail { get; set; } = true;
    public bool NotifyShiftRemovedSms { get; set; } = true;
    public bool NotifyShiftBoundaryChangedEmail { get; set; } = true;
    public bool NotifyShiftBoundaryChangedSms { get; set; } = true;
    public bool NotifyShiftReminderEmail { get; set; } = true;
    public bool NotifyShiftReminderSms { get; set; } = true;
}

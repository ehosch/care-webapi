namespace Care.WebApi.Host.Controllers;

public record UpdateSettingsRequest(
    string? PatientName,
    bool NotifyShiftAssignedEmail = true,
    bool NotifyShiftAssignedSms = true,
    bool NotifyReplacementRequestedEmail = true,
    bool NotifyReplacementRequestedSms = true,
    bool NotifyReplacementClaimedEmail = true,
    bool NotifyReplacementClaimedSms = true,
    bool NotifyDocumentUploadedEmail = true,
    bool NotifyDocumentUploadedSms = true,
    bool NotifyShiftRemovedEmail = true,
    bool NotifyShiftRemovedSms = true,
    bool NotifyShiftBoundaryChangedEmail = true,
    bool NotifyShiftBoundaryChangedSms = true,
    bool NotifyShiftReminderEmail = true,
    bool NotifyShiftReminderSms = true);

namespace Care.WebApi.Application.Common.Settings;

public record AppSettingsDto(
    string? PatientName,
    bool NotifyShiftAssignedEmail,
    bool NotifyShiftAssignedSms,
    bool NotifyReplacementRequestedEmail,
    bool NotifyReplacementRequestedSms,
    bool NotifyReplacementClaimedEmail,
    bool NotifyReplacementClaimedSms,
    bool NotifyDocumentUploadedEmail,
    bool NotifyDocumentUploadedSms,
    bool NotifyShiftRemovedEmail,
    bool NotifyShiftRemovedSms,
    bool NotifyShiftBoundaryChangedEmail,
    bool NotifyShiftBoundaryChangedSms,
    bool NotifyShiftReminderEmail,
    bool NotifyShiftReminderSms);

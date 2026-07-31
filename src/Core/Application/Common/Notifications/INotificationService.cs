using Care.WebApi.Domain.Care;

namespace Care.WebApi.Application.Common.Notifications;

public interface INotificationService
{
    Task NotifyShiftAssignedAsync(string userId, DateOnly date, ShiftType shiftType, CancellationToken cancellationToken);

    Task NotifyReplacementRequestedAsync(DateOnly date, ShiftType shiftType, string requestedByUserId, string? reason, CancellationToken cancellationToken);

    Task NotifyReplacementClaimedAsync(string requesterUserId, string claimedByUserId, DateOnly date, ShiftType shiftType, CancellationToken cancellationToken);

    Task NotifyDocumentUploadedAsync(string title, string category, string uploadedByUserId, CancellationToken cancellationToken);

    Task NotifyScheduleGapAsync(string affectedUserId, DateOnly affectedDate, ShiftType affectedShiftType, int gapMinutes, CancellationToken cancellationToken);
}

namespace Care.WebApi.Application.Common.Notifications;

public interface INotificationService
{
    Task NotifyShiftAssignedAsync(string userId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken);

    Task NotifyReplacementRequestedAsync(DateOnly date, TimeSpan startTime, TimeSpan endTime, string requestedByUserId, string? reason, CancellationToken cancellationToken);

    Task NotifyReplacementClaimedAsync(string requesterUserId, string claimedByUserId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken);

    Task NotifyDocumentUploadedAsync(string title, string category, string uploadedByUserId, CancellationToken cancellationToken);

    Task NotifyShiftRemovedAsync(string affectedUserId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken);

    Task NotifyShiftBoundaryChangedAsync(string affectedUserId, DateOnly date, TimeSpan newStartTime, TimeSpan newEndTime, CancellationToken cancellationToken);
}

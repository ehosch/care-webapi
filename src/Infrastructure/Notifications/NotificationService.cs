using Care.WebApi.Application.Common.Mailing;
using Care.WebApi.Application.Common.Notifications;
using Care.WebApi.Application.Common.Sms;
using Care.WebApi.Domain.Care;
using Care.WebApi.Infrastructure.Identity;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Care.WebApi.Infrastructure.Notifications;

internal class NotificationService : INotificationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBackgroundJobClient _jobClient;

    public NotificationService(UserManager<ApplicationUser> userManager, IBackgroundJobClient jobClient)
    {
        _userManager = userManager;
        _jobClient = jobClient;
    }

    public async Task NotifyShiftAssignedAsync(string userId, DateOnly date, ShiftType shiftType, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        EnqueueNotification(
            user,
            "You've been assigned a shift",
            NotificationTemplates.ShiftAssignedEmail(date, shiftType),
            NotificationTemplates.ShiftAssignedSms(date, shiftType));
    }

    public async Task NotifyReplacementRequestedAsync(DateOnly date, ShiftType shiftType, string requestedByUserId, string? reason, CancellationToken cancellationToken)
    {
        string requestedByName = await GetNameAsync(requestedByUserId);

        foreach (var user in await GetOtherActiveUsersAsync(requestedByUserId, cancellationToken))
        {
            EnqueueNotification(
                user,
                "Replacement requested",
                NotificationTemplates.ReplacementRequestedEmail(date, shiftType, requestedByName, reason),
                NotificationTemplates.ReplacementRequestedSms(date, shiftType, requestedByName));
        }
    }

    public async Task NotifyReplacementClaimedAsync(string requesterUserId, string claimedByUserId, DateOnly date, ShiftType shiftType, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(requesterUserId) is not { } requester)
        {
            return;
        }

        string claimedByName = await GetNameAsync(claimedByUserId);

        EnqueueNotification(
            requester,
            "Your shift replacement was covered",
            NotificationTemplates.ReplacementClaimedEmail(date, shiftType, claimedByName),
            NotificationTemplates.ReplacementClaimedSms(date, shiftType, claimedByName));
    }

    public async Task NotifyDocumentUploadedAsync(string title, string category, string uploadedByUserId, CancellationToken cancellationToken)
    {
        string uploadedByName = await GetNameAsync(uploadedByUserId);

        foreach (var user in await GetOtherActiveUsersAsync(uploadedByUserId, cancellationToken))
        {
            EnqueueNotification(
                user,
                "New document uploaded",
                NotificationTemplates.DocumentUploadedEmail(title, category, uploadedByName),
                NotificationTemplates.DocumentUploadedSms(title, uploadedByName));
        }
    }

    public async Task NotifyScheduleGapAsync(string affectedUserId, DateOnly affectedDate, ShiftType affectedShiftType, int gapMinutes, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(affectedUserId) is not { } user)
        {
            return;
        }

        EnqueueNotification(
            user,
            "A schedule gap opened up near your shift",
            NotificationTemplates.ScheduleGapEmail(affectedDate, affectedShiftType, gapMinutes),
            NotificationTemplates.ScheduleGapSms(affectedDate, affectedShiftType, gapMinutes));
    }

    private void EnqueueNotification(ApplicationUser user, string subject, string emailBody, string smsBody)
    {
        _jobClient.Enqueue<IMailService>(m => m.SendAsync(
            new MailRequest(new List<string> { user.Email! }, subject, emailBody),
            CancellationToken.None));

        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            _jobClient.Enqueue<ISmsService>(s => s.SendAsync(
                new SmsRequest(user.PhoneNumber, smsBody),
                CancellationToken.None));
        }
    }

    private async Task<List<ApplicationUser>> GetOtherActiveUsersAsync(string excludeUserId, CancellationToken cancellationToken) =>
        await _userManager.Users
            .Where(u => u.Status == UserStatus.Active && u.Id != excludeUserId)
            .ToListAsync(cancellationToken);

    private async Task<string> GetNameAsync(string userId) =>
        await _userManager.FindByIdAsync(userId) is { } user ? user.Name : "Unknown";
}

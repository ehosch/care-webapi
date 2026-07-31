using Care.WebApi.Application.Common.Mailing;
using Care.WebApi.Application.Common.Notifications;
using Care.WebApi.Application.Common.Settings;
using Care.WebApi.Application.Common.Sms;
using Care.WebApi.Infrastructure.Identity;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Care.WebApi.Infrastructure.Notifications;

internal class NotificationService : INotificationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IAppSettingsService _appSettingsService;
    private readonly string? _frontendBaseUrl;

    public NotificationService(UserManager<ApplicationUser> userManager, IBackgroundJobClient jobClient, IAppSettingsService appSettingsService, IConfiguration config)
    {
        _userManager = userManager;
        _jobClient = jobClient;
        _appSettingsService = appSettingsService;
        _frontendBaseUrl = config["CorsSettings:Blazor"]
            ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.TrimEnd('/');
    }

    public async Task NotifyShiftAssignedAsync(string userId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
        EnqueueNotification(
            user,
            "You've been assigned a shift",
            NotificationTemplates.ShiftAssignedEmail(date, startTime, endTime),
            NotificationTemplates.ShiftAssignedSms(date, startTime, endTime),
            settings.NotifyShiftAssignedEmail,
            settings.NotifyShiftAssignedSms);
    }

    public async Task NotifyReplacementRequestedAsync(DateOnly date, TimeSpan startTime, TimeSpan endTime, string requestedByUserId, string? reason, CancellationToken cancellationToken)
    {
        string requestedByName = await GetNameAsync(requestedByUserId);
        string link = $"{_frontendBaseUrl}/replacement-requests";
        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);

        foreach (var user in await GetOtherActiveUsersAsync(requestedByUserId, cancellationToken))
        {
            EnqueueNotification(
                user,
                "Replacement requested",
                NotificationTemplates.ReplacementRequestedEmail(date, startTime, endTime, requestedByName, reason, link),
                NotificationTemplates.ReplacementRequestedSms(date, startTime, endTime, requestedByName, link),
                settings.NotifyReplacementRequestedEmail,
                settings.NotifyReplacementRequestedSms);
        }
    }

    public async Task NotifyReplacementClaimedAsync(string requesterUserId, string claimedByUserId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(requesterUserId) is not { } requester)
        {
            return;
        }

        string claimedByName = await GetNameAsync(claimedByUserId);
        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);

        EnqueueNotification(
            requester,
            "Your shift replacement was covered",
            NotificationTemplates.ReplacementClaimedEmail(date, startTime, endTime, claimedByName),
            NotificationTemplates.ReplacementClaimedSms(date, startTime, endTime, claimedByName),
            settings.NotifyReplacementClaimedEmail,
            settings.NotifyReplacementClaimedSms);
    }

    public async Task NotifyDocumentUploadedAsync(string title, string category, string uploadedByUserId, CancellationToken cancellationToken)
    {
        string uploadedByName = await GetNameAsync(uploadedByUserId);
        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);

        foreach (var user in await GetOtherActiveUsersAsync(uploadedByUserId, cancellationToken))
        {
            EnqueueNotification(
                user,
                "New document uploaded",
                NotificationTemplates.DocumentUploadedEmail(title, category, uploadedByName),
                NotificationTemplates.DocumentUploadedSms(title, uploadedByName),
                settings.NotifyDocumentUploadedEmail,
                settings.NotifyDocumentUploadedSms);
        }
    }

    public async Task NotifyShiftRemovedAsync(string affectedUserId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(affectedUserId) is not { } user)
        {
            return;
        }

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
        EnqueueNotification(
            user,
            "Your shift was removed",
            NotificationTemplates.ShiftRemovedEmail(date, startTime, endTime),
            NotificationTemplates.ShiftRemovedSms(date, startTime, endTime),
            settings.NotifyShiftRemovedEmail,
            settings.NotifyShiftRemovedSms);
    }

    public async Task NotifyShiftBoundaryChangedAsync(string affectedUserId, DateOnly date, TimeSpan newStartTime, TimeSpan newEndTime, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(affectedUserId) is not { } user)
        {
            return;
        }

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
        EnqueueNotification(
            user,
            "Your shift's time changed",
            NotificationTemplates.ShiftBoundaryChangedEmail(date, newStartTime, newEndTime),
            NotificationTemplates.ShiftBoundaryChangedSms(date, newStartTime, newEndTime),
            settings.NotifyShiftBoundaryChangedEmail,
            settings.NotifyShiftBoundaryChangedSms);
    }

    public async Task NotifyShiftReminderAsync(string userId, DateOnly date, TimeSpan startTime, TimeSpan endTime, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
        EnqueueNotification(
            user,
            "Your shift starts in about an hour",
            NotificationTemplates.ShiftReminderEmail(date, startTime, endTime),
            NotificationTemplates.ShiftReminderSms(date, startTime, endTime),
            settings.NotifyShiftReminderEmail,
            settings.NotifyShiftReminderSms);
    }

    private void EnqueueNotification(ApplicationUser user, string subject, string emailBody, string smsBody, bool emailEnabled, bool smsEnabled)
    {
        if (emailEnabled)
        {
            _jobClient.Enqueue<IMailService>(m => m.SendAsync(
                new MailRequest(new List<string> { user.Email! }, subject, emailBody),
                CancellationToken.None));
        }

        if (smsEnabled && !string.IsNullOrEmpty(user.PhoneNumber))
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

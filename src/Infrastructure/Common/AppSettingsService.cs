using Care.WebApi.Application.Common.Settings;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Care.WebApi.Infrastructure.Common;

internal class AppSettingsService : IAppSettingsService
{
    private readonly ApplicationDbContext _db;

    public AppSettingsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task UpdateSettingsAsync(AppSettingsDto settings, CancellationToken cancellationToken)
    {
        var entity = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            entity = new Domain.Common.AppSettings();
            _db.AppSettings.Add(entity);
        }

        entity.PatientName = settings.PatientName;
        entity.NotifyShiftAssignedEmail = settings.NotifyShiftAssignedEmail;
        entity.NotifyShiftAssignedSms = settings.NotifyShiftAssignedSms;
        entity.NotifyReplacementRequestedEmail = settings.NotifyReplacementRequestedEmail;
        entity.NotifyReplacementRequestedSms = settings.NotifyReplacementRequestedSms;
        entity.NotifyReplacementClaimedEmail = settings.NotifyReplacementClaimedEmail;
        entity.NotifyReplacementClaimedSms = settings.NotifyReplacementClaimedSms;
        entity.NotifyDocumentUploadedEmail = settings.NotifyDocumentUploadedEmail;
        entity.NotifyDocumentUploadedSms = settings.NotifyDocumentUploadedSms;
        entity.NotifyShiftRemovedEmail = settings.NotifyShiftRemovedEmail;
        entity.NotifyShiftRemovedSms = settings.NotifyShiftRemovedSms;
        entity.NotifyShiftBoundaryChangedEmail = settings.NotifyShiftBoundaryChangedEmail;
        entity.NotifyShiftBoundaryChangedSms = settings.NotifyShiftBoundaryChangedSms;
        entity.NotifyShiftReminderEmail = settings.NotifyShiftReminderEmail;
        entity.NotifyShiftReminderSms = settings.NotifyShiftReminderSms;
        entity.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AppSettingsDto ToDto(Domain.Common.AppSettings? settings) => new(
        settings?.PatientName,
        settings?.NotifyShiftAssignedEmail ?? true,
        settings?.NotifyShiftAssignedSms ?? true,
        settings?.NotifyReplacementRequestedEmail ?? true,
        settings?.NotifyReplacementRequestedSms ?? true,
        settings?.NotifyReplacementClaimedEmail ?? true,
        settings?.NotifyReplacementClaimedSms ?? true,
        settings?.NotifyDocumentUploadedEmail ?? true,
        settings?.NotifyDocumentUploadedSms ?? true,
        settings?.NotifyShiftRemovedEmail ?? true,
        settings?.NotifyShiftRemovedSms ?? true,
        settings?.NotifyShiftBoundaryChangedEmail ?? true,
        settings?.NotifyShiftBoundaryChangedSms ?? true,
        settings?.NotifyShiftReminderEmail ?? true,
        settings?.NotifyShiftReminderSms ?? true);
}

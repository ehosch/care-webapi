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
        return new AppSettingsDto(settings?.PatientName);
    }

    public async Task UpdateSettingsAsync(string? patientName, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new Domain.Common.AppSettings();
            _db.AppSettings.Add(settings);
        }

        settings.PatientName = patientName;
        settings.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

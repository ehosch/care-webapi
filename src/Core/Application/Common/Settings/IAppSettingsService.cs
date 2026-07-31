namespace Care.WebApi.Application.Common.Settings;

public interface IAppSettingsService
{
    Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken);

    Task UpdateSettingsAsync(string? patientName, CancellationToken cancellationToken);
}

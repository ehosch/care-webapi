namespace Care.WebApi.Application.Common.Sms;

public interface ISmsService
{
    Task SendAsync(SmsRequest request, CancellationToken cancellationToken = default);
}

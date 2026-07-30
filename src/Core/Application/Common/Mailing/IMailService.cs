namespace Care.WebApi.Application.Common.Mailing;

public interface IMailService
{
    Task SendAsync(MailRequest request, CancellationToken cancellationToken = default);
}

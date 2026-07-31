namespace Care.WebApi.Infrastructure.Care;

public interface IShiftReminderJob
{
    Task RunAsync(CancellationToken cancellationToken);
}

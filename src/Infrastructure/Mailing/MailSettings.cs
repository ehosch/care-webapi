namespace Care.WebApi.Infrastructure.Mailing;

public class MailSettings
{
    public string DisplayName { get; set; } = default!;
    public string From { get; set; } = default!;
    public string Host { get; set; } = default!;
    public int Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

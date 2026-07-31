namespace Care.WebApi.Infrastructure.Sms;

public class SmsSettings
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromNumber { get; set; }
}

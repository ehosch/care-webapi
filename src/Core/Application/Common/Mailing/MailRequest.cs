namespace Care.WebApi.Application.Common.Mailing;

public record MailRequest(List<string> To, string Subject, string Body);

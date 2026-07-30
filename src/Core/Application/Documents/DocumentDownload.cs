namespace Care.WebApi.Application.Documents;

public record DocumentDownload(Stream Content, string FileName, string ContentType);

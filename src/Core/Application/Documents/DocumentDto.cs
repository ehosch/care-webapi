namespace Care.WebApi.Application.Documents;

public record DocumentDto(
    Guid Id,
    string Title,
    string Category,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    int Version,
    string UploadedByName,
    DateTime UploadedAt);

public record DocumentVersionDto(
    int Version,
    string FileName,
    long FileSizeBytes,
    string UploadedByName,
    DateTime UploadedAt);

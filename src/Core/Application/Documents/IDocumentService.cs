namespace Care.WebApi.Application.Documents;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken);

    Task<List<DocumentVersionDto>> GetVersionHistoryAsync(Guid documentId, CancellationToken cancellationToken);

    Task<Guid> UploadAsync(
        string title,
        string category,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        string uploadedByUserId,
        CancellationToken cancellationToken);

    Task ReplaceAsync(
        Guid documentId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        string uploadedByUserId,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken);

    Task<DocumentDownload> DownloadCurrentAsync(Guid documentId, CancellationToken cancellationToken);

    Task<DocumentDownload> DownloadVersionAsync(Guid documentId, int version, CancellationToken cancellationToken);
}

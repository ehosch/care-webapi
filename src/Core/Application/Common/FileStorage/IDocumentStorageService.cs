namespace Care.WebApi.Application.Common.FileStorage;

public interface IDocumentStorageService
{
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    void Delete(string storageKey);
}

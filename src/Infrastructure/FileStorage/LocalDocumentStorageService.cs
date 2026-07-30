using Care.WebApi.Application.Common.FileStorage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Care.WebApi.Infrastructure.FileStorage;

public class LocalDocumentStorageService : IDocumentStorageService
{
    private readonly string _root;

    public LocalDocumentStorageService(IOptions<DocumentStorageSettings> settings, IHostEnvironment env)
    {
        _root = string.IsNullOrWhiteSpace(settings.Value.StoragePath)
            ? Path.Combine(env.ContentRootPath, "App_Data", "documents")
            : settings.Value.StoragePath;

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        string key = Guid.NewGuid().ToString("N");
        string path = Path.Combine(_root, key);

        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_root, Path.GetFileName(storageKey));
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public void Delete(string storageKey)
    {
        string path = Path.Combine(_root, Path.GetFileName(storageKey));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

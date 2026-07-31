using Care.WebApi.Application.Common.Exceptions;
using Care.WebApi.Application.Common.FileStorage;
using Care.WebApi.Application.Common.Notifications;
using Care.WebApi.Application.Documents;
using Care.WebApi.Domain.Documents;
using Care.WebApi.Infrastructure.Identity;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Care.WebApi.Infrastructure.Documents;

internal class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorageService _storage;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext db,
        IDocumentStorageService storage,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _userManager = userManager;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        var documents = await _db.Documents.OrderBy(d => d.Title).ToListAsync(cancellationToken);
        var names = await ResolveNamesAsync(documents.Select(d => d.UploadedByUserId));

        return documents
            .Select(d => new DocumentDto(
                d.Id,
                d.Title,
                d.Category,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.Version,
                names.GetValueOrDefault(d.UploadedByUserId, "Unknown"),
                d.LastModifiedOn ?? d.CreatedOn))
            .ToList();
    }

    public async Task<List<DocumentVersionDto>> GetVersionHistoryAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var versions = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
        var names = await ResolveNamesAsync(versions.Select(v => v.UploadedByUserId));

        return versions
            .Select(v => new DocumentVersionDto(
                v.Version,
                v.FileName,
                v.FileSizeBytes,
                names.GetValueOrDefault(v.UploadedByUserId, "Unknown"),
                v.CreatedOn))
            .ToList();
    }

    public async Task<Guid> UploadAsync(
        string title,
        string category,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        string uploadedByUserId,
        CancellationToken cancellationToken)
    {
        string storageKey = await _storage.SaveAsync(content, cancellationToken);

        var document = new Document
        {
            Title = title,
            Category = category,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            FilePath = storageKey,
            UploadedByUserId = uploadedByUserId,
            Version = 1
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyDocumentUploadedAsync(title, category, uploadedByUserId, cancellationToken);

        return document.Id;
    }

    public async Task ReplaceAsync(
        Guid documentId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        string uploadedByUserId,
        CancellationToken cancellationToken)
    {
        var document = await _db.Documents.FindAsync([documentId], cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        string newStorageKey = await _storage.SaveAsync(content, cancellationToken);

        // Archive the outgoing version — its file stays on disk untouched, just no longer "current".
        _db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = document.Id,
            Version = document.Version,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FilePath = document.FilePath,
            FileSizeBytes = document.FileSizeBytes,
            UploadedByUserId = document.UploadedByUserId
        });

        document.FileName = fileName;
        document.ContentType = contentType;
        document.FileSizeBytes = fileSizeBytes;
        document.FilePath = newStorageKey;
        document.UploadedByUserId = uploadedByUserId;
        document.Version += 1;
        document.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents.FindAsync([documentId], cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        var versions = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        var storageKeys = new List<string> { document.FilePath };
        storageKeys.AddRange(versions.Select(v => v.FilePath));

        _db.DocumentVersions.RemoveRange(versions);
        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (string key in storageKeys)
        {
            try
            {
                _storage.Delete(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete stored file {StorageKey} for document {DocumentId}", key, documentId);
            }
        }
    }

    public async Task<DocumentDownload> DownloadCurrentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents.FindAsync([documentId], cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        var stream = await _storage.OpenReadAsync(document.FilePath, cancellationToken);
        return new DocumentDownload(stream, document.FileName, document.ContentType);
    }

    public async Task<DocumentDownload> DownloadVersionAsync(Guid documentId, int version, CancellationToken cancellationToken)
    {
        var documentVersion = await _db.DocumentVersions
            .FirstOrDefaultAsync(v => v.DocumentId == documentId && v.Version == version, cancellationToken)
            ?? throw new NotFoundException("Document version not found.");

        var stream = await _storage.OpenReadAsync(documentVersion.FilePath, cancellationToken);
        return new DocumentDownload(stream, documentVersion.FileName, documentVersion.ContentType);
    }

    private async Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<string> userIds)
    {
        var names = new Dictionary<string, string>();
        foreach (string userId in userIds.Distinct())
        {
            if (await _userManager.FindByIdAsync(userId) is { } user)
            {
                names[userId] = user.Name;
            }
        }

        return names;
    }
}

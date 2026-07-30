using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Documents;

public class DocumentVersion : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = default!;
}

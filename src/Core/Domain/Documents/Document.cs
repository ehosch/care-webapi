using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Documents;

public class Document : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public string UploadedByUserId { get; set; } = default!;
    public int Version { get; set; } = 1;
}

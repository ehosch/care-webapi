namespace Care.WebApi.Domain.Common.Contracts;

public abstract class AuditableEntity : BaseEntity
{
    public string? CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }

    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}

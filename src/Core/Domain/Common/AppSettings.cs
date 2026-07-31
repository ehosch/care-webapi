using Care.WebApi.Domain.Common.Contracts;

namespace Care.WebApi.Domain.Common;

public class AppSettings : AuditableEntity
{
    public string? PatientName { get; set; }
}

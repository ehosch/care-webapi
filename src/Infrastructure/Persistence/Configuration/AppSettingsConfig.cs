using Care.WebApi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class AppSettingsConfig : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.Property(s => s.PatientName).HasMaxLength(256);
    }
}

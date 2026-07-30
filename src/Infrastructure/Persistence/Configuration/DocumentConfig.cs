using Care.WebApi.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class DocumentConfig : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.Title).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Category).HasMaxLength(128).IsRequired();
        builder.Property(d => d.FilePath).HasMaxLength(1024).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(d => d.UploadedByUserId).HasMaxLength(450).IsRequired();
    }
}

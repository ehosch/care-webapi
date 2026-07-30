using Care.WebApi.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class DocumentVersionConfig : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.Property(v => v.FileName).HasMaxLength(260).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(v => v.FilePath).HasMaxLength(1024).IsRequired();
        builder.Property(v => v.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(v => v.DocumentId);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

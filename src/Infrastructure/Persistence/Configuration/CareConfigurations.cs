using Care.WebApi.Domain.Care;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class ShiftConfig : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(s => s.AssignedUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(s => s.Date);
    }
}

public class ReplacementRequestConfig : IEntityTypeConfiguration<ReplacementRequest>
{
    public void Configure(EntityTypeBuilder<ReplacementRequest> builder)
    {
        builder.Property(r => r.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.ClaimedByUserId).HasMaxLength(450);
        builder.HasIndex(r => r.Status);
    }
}

public class ShiftNoteConfig : IEntityTypeConfiguration<ShiftNote>
{
    public void Configure(EntityTypeBuilder<ShiftNote> builder)
    {
        builder.Property(n => n.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Text).HasMaxLength(4000).IsRequired();
        builder.HasIndex(n => n.ShiftId);
    }
}

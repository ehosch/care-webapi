using Care.WebApi.Domain.Care;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class ShiftTemplateConfig : IEntityTypeConfiguration<ShiftTemplate>
{
    public void Configure(EntityTypeBuilder<ShiftTemplate> builder)
    {
    }
}

public class ShiftConfig : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(s => s.AssignedUserId).HasMaxLength(450);
        builder.HasIndex(s => s.Date);
        builder.HasIndex(s => new { s.Date, s.ShiftType }).IsUnique();
    }
}

public class ReplacementRequestConfig : IEntityTypeConfiguration<ReplacementRequest>
{
    public void Configure(EntityTypeBuilder<ReplacementRequest> builder)
    {
        builder.Property(r => r.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.ClaimedByUserId).HasMaxLength(450);
    }
}

public class ShiftNoteConfig : IEntityTypeConfiguration<ShiftNote>
{
    public void Configure(EntityTypeBuilder<ShiftNote> builder)
    {
        builder.Property(n => n.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Text).HasMaxLength(4000).IsRequired();
    }
}

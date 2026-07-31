using Care.WebApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class InviteConfig : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.Property(i => i.UserId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.Email).HasMaxLength(256);
        builder.Property(i => i.Token).HasMaxLength(512).IsRequired();
        builder.Property(i => i.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(i => i.Token).IsUnique();
    }
}

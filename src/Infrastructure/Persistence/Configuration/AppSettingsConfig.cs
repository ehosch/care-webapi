using Care.WebApi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Care.WebApi.Infrastructure.Persistence.Configuration;

public class AppSettingsConfig : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.Property(s => s.PatientName).HasMaxLength(256);

        builder.Property(s => s.NotifyShiftAssignedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftAssignedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyReplacementRequestedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyReplacementRequestedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyReplacementClaimedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyReplacementClaimedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyDocumentUploadedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyDocumentUploadedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftRemovedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftRemovedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftBoundaryChangedEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftBoundaryChangedSms).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftReminderEmail).HasDefaultValue(true);
        builder.Property(s => s.NotifyShiftReminderSms).HasDefaultValue(true);
    }
}

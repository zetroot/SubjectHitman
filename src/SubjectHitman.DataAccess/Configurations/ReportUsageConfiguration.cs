using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="ReportUsage"/>.
/// </summary>
public sealed class ReportUsageConfiguration : IEntityTypeConfiguration<ReportUsage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportUsage> builder)
    {
        builder.ToTable("report_usages");
        builder.HasKey(x => x.ReportId);
        builder.Property(x => x.ReportId).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<short>().IsRequired();
        builder.Property(x => x.OrderedAt).IsRequired();
        builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SubjectId, x.OrderedAt })
            .HasDatabaseName("ix_report_usages_count")
            .HasFilter("is_free AND status = 1");
    }
}

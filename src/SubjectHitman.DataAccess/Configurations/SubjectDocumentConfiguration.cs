using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="SubjectDocument"/>.
/// </summary>
public sealed class SubjectDocumentConfiguration : IEntityTypeConfiguration<SubjectDocument>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectDocument> builder)
    {
        builder.ToTable("subject_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.TypeCode).IsRequired();
        builder.Property(x => x.Series).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(x => x.Number).IsRequired();
        builder.HasIndex(x => new { x.SubjectId, x.TypeCode, x.Series, x.Number, x.IssueDate })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}

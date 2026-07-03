using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="SubjectName"/>.
/// </summary>
public sealed class SubjectNameConfiguration : IEntityTypeConfiguration<SubjectName>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectName> builder)
    {
        builder.ToTable("subject_names");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.LastName).IsRequired();
        builder.Property(x => x.FirstName).IsRequired();
        builder.Property(x => x.MiddleName).IsRequired();
        builder.HasIndex(x => new { x.SubjectId, x.LastName, x.FirstName, x.MiddleName }).IsUnique();
    }
}

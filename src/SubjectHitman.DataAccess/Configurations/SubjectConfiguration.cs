using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="Subject"/>.
/// </summary>
public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Inn).HasMaxLength(12);
        builder.Property(x => x.Snils).HasMaxLength(11);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasMany(x => x.Names).WithOne().HasForeignKey(n => n.SubjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documents).WithOne().HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.SearchKeys).WithOne().HasForeignKey(k => k.SubjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

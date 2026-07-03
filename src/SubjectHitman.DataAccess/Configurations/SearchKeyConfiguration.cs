using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности <see cref="SearchKey"/>.
/// </summary>
public sealed class SearchKeyConfiguration : IEntityTypeConfiguration<SearchKey>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SearchKey> builder)
    {
        builder.ToTable("search_keys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.KeyType).HasConversion<short>().IsRequired();
        builder.Property(x => x.Hash).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.SubjectId, x.KeyType, x.Hash }).IsUnique();
        builder.HasIndex(x => x.Hash);
    }
}

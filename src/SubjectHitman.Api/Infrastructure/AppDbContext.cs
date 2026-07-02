using Microsoft.EntityFrameworkCore;
using SubjectHitman.Api.Domain.Entities;

namespace SubjectHitman.Api.Infrastructure;

/// <summary>
/// Контекст базы данных EF Core компонента SubjectHitman (PostgreSQL).
/// </summary>
/// <param name="options">Параметры контекста.</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Субъекты кредитных историй (основные записи).</summary>
    public DbSet<Subject> Subjects => Set<Subject>();

    /// <summary>Полные имена субъектов.</summary>
    public DbSet<SubjectName> SubjectNames => Set<SubjectName>();

    /// <summary>Документы, удостоверяющие личность субъектов.</summary>
    public DbSet<SubjectDocument> SubjectDocuments => Set<SubjectDocument>();

    /// <summary>Предварительно вычисленные ключи поиска.</summary>
    public DbSet<SearchKey> SearchKeys => Set<SearchKey>();

    /// <summary>Учётные записи отчётов.</summary>
    public DbSet<ReportUsage> ReportUsages => Set<ReportUsage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(e =>
        {
            e.ToTable("subjects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Inn).HasMaxLength(12);
            e.Property(x => x.Snils).HasMaxLength(11);
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasMany(x => x.Names).WithOne().HasForeignKey(n => n.SubjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SearchKeys).WithOne().HasForeignKey(k => k.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubjectName>(e =>
        {
            e.ToTable("subject_names");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.LastName).IsRequired();
            e.Property(x => x.FirstName).IsRequired();
            e.Property(x => x.MiddleName).IsRequired();
            e.HasIndex(x => new { x.SubjectId, x.LastName, x.FirstName, x.MiddleName }).IsUnique();
        });

        modelBuilder.Entity<SubjectDocument>(e =>
        {
            e.ToTable("subject_documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.TypeCode).IsRequired();
            e.Property(x => x.Series).IsRequired().HasDefaultValue(string.Empty);
            e.Property(x => x.Number).IsRequired();
            e.HasIndex(x => new { x.SubjectId, x.TypeCode, x.Series, x.Number, x.IssueDate })
                .IsUnique()
                .AreNullsDistinct(false);
        });

        modelBuilder.Entity<SearchKey>(e =>
        {
            e.ToTable("search_keys");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.KeyType).HasConversion<short>().IsRequired();
            e.Property(x => x.Hash).IsRequired().HasMaxLength(32);
            e.HasIndex(x => new { x.SubjectId, x.KeyType, x.Hash }).IsUnique();
            e.HasIndex(x => x.Hash);
        });

        modelBuilder.Entity<ReportUsage>(e =>
        {
            e.ToTable("report_usages");
            e.HasKey(x => x.ReportId);
            e.Property(x => x.ReportId).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<short>().IsRequired();
            e.Property(x => x.OrderedAt).IsRequired();
            e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SubjectId, x.OrderedAt })
                .HasDatabaseName("ix_report_usages_count")
                .HasFilter("is_free AND status = 1");
        });

        modelBuilder.HasDefaultSchema(null);
        base.OnModelCreating(modelBuilder);
    }
}

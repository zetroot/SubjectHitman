using Microsoft.EntityFrameworkCore;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.DataAccess;

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.HasDefaultSchema(null);
        base.OnModelCreating(modelBuilder);
    }
}

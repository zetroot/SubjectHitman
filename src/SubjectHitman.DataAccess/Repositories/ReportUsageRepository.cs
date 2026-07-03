using Microsoft.EntityFrameworkCore;
using SubjectHitman.Domain.Entities;
using SubjectHitman.Domain.Repositories;

namespace SubjectHitman.DataAccess.Repositories;

/// <summary>
/// Реализация <see cref="IReportUsageRepository"/> поверх <see cref="AppDbContext"/>.
/// </summary>
/// <param name="dbContext">Контекст базы данных.</param>
public sealed class ReportUsageRepository(AppDbContext dbContext) : IReportUsageRepository
{
    /// <inheritdoc />
    public async Task<ReportUsage?> FindAsync(Guid reportId, CancellationToken ct)
    {
        return await dbContext.ReportUsages.FindAsync([reportId], ct);
    }

    /// <inheritdoc />
    public void Add(ReportUsage usage)
    {
        dbContext.ReportUsages.Add(usage);
    }

    /// <inheritdoc />
    public async Task<List<DateTimeOffset>> GetChargedFreeOrderTimestampsAsync(
        Guid subjectId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        return await dbContext.ReportUsages
            .Where(r => r.SubjectId == subjectId
                        && r.IsFree
                        && r.Status == ReportUsageStatus.Charged
                        && r.OrderedAt >= fromUtc
                        && r.OrderedAt < toUtc)
            .OrderBy(r => r.OrderedAt)
            .Select(r => r.OrderedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Result of a free-report usage count.
/// </summary>
/// <param name="UsedFreeReportsCount">Number of cooldown-collapsed charged free reports in the period.</param>
/// <param name="PeriodStart">Inclusive start of the calendar-year period.</param>
/// <param name="PeriodEnd">Inclusive end of the calendar-year period (last second of the year).</param>
public record FreeReportCountResult(int UsedFreeReportsCount, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);

/// <summary>
/// Counts free reports charged to a subject within the current calendar year,
/// collapsing reports inside the cooldown period into one (technical spec, § 6).
/// </summary>
/// <param name="dbContext">Database context.</param>
/// <param name="options">Counting options (cooldown, time zone).</param>
/// <param name="timeProvider">Time provider defining "now".</param>
public class FreeReportCounter(
    AppDbContext dbContext,
    IOptions<FreeReportsOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Counts cooldown-collapsed charged free reports of the subject for the current calendar year.
    /// </summary>
    /// <param name="subjectId">Internal subject identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count and the calendar-year period boundaries.</returns>
    public async Task<FreeReportCountResult> CountAsync(Guid subjectId, CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var nowLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz);
        var startLocal = new DateTimeOffset(nowLocal.Year, 1, 1, 0, 0, 0, nowLocal.Offset);
        // Recompute the offset at the period boundary itself (DST-safe for arbitrary zones).
        var startUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), tz),
            TimeSpan.Zero);
        var endUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), tz),
            TimeSpan.Zero);

        var orderedAts = await dbContext.ReportUsages
            .Where(r => r.SubjectId == subjectId
                        && r.IsFree
                        && r.Status == ReportUsageStatus.Charged
                        && r.OrderedAt >= startUtc
                        && r.OrderedAt < endUtc)
            .OrderBy(r => r.OrderedAt)
            .Select(r => r.OrderedAt)
            .ToListAsync(ct);

        var count = CollapseByCooldown(orderedAts, options.Value.CooldownPeriod);

        return new FreeReportCountResult(
            count,
            TimeZoneInfo.ConvertTime(startUtc, tz),
            TimeZoneInfo.ConvertTime(endUtc - TimeSpan.FromSeconds(1), tz));
    }

    /// <summary>
    /// Collapses a chronologically sorted sequence of report timestamps into groups:
    /// a report belongs to the current group when it is within <paramref name="cooldown"/>
    /// of the FIRST report of the group (strictly greater difference opens a new group).
    /// </summary>
    /// <param name="orderedTimestamps">Timestamps sorted ascending.</param>
    /// <param name="cooldown">Cooldown period.</param>
    /// <returns>The number of groups.</returns>
    public static int CollapseByCooldown(IReadOnlyList<DateTimeOffset> orderedTimestamps, TimeSpan cooldown)
    {
        ArgumentNullException.ThrowIfNull(orderedTimestamps);

        var count = 0;
        DateTimeOffset? groupStart = null;
        foreach (var ts in orderedTimestamps)
        {
            if (groupStart is null || ts - groupStart.Value > cooldown)
            {
                count++;
                groupStart = ts;
            }
        }

        return count;
    }
}

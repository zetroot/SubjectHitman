using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Handles the <see cref="ReportCompleted"/> event: marks the report as charged.
/// If the report is unknown (race with <see cref="ReportOrdered"/>), the event is logged and discarded (Q2).
/// </summary>
public static class ReportCompletedHandler
{
    /// <summary>
    /// Processes the report completed event.
    /// </summary>
    /// <param name="message">The report completed event.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task Handle(
        ReportCompleted message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var usage = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (usage is null)
        {
            logger.LogWarning("ReportCompleted for unknown report {ReportId}, discarding", message.ReportId);
            return;
        }

        if (usage.Status != ReportUsageStatus.Pending)
        {
            logger.LogDebug("Report {ReportId} already in final status {Status}, ignoring Completed", message.ReportId, usage.Status);
            return;
        }

        usage.Status = ReportUsageStatus.Charged;
        usage.FinishedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report {ReportId} accounted as charged", message.ReportId);
    }
}

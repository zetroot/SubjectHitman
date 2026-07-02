using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Handles the <see cref="ReportFailed"/> event: marks the report as not charged.
/// If the report is unknown, the event is logged and discarded (Q2).
/// </summary>
public static class ReportFailedHandler
{
    /// <summary>
    /// Processes the report failed event.
    /// </summary>
    /// <param name="message">The report failed event.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task Handle(
        ReportFailed message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var usage = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (usage is null)
        {
            logger.LogWarning("ReportFailed for unknown report {ReportId}, discarding", message.ReportId);
            return;
        }

        if (usage.Status != ReportUsageStatus.Pending)
        {
            logger.LogDebug("Report {ReportId} already in final status {Status}, ignoring Failed", message.ReportId, usage.Status);
            return;
        }

        usage.Status = ReportUsageStatus.NotCharged;
        usage.FinishedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report {ReportId} accounted as not charged (reason: {Reason})", message.ReportId, message.Reason ?? "-");
    }
}

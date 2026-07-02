using Microsoft.Extensions.Options;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;
using Wolverine;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Scheduled handler that checks the status of a pending report via the external status API
/// and either finalises the report accounting or re-schedules another check (up to <c>MaxTimeoutRetries</c> times).
/// </summary>
public static class ReportStatusCheckTimeoutHandler
{
    /// <summary>
    /// Processes the scheduled timeout: queries the external status API and acts on the result.
    /// </summary>
    /// <param name="message">The timeout message with the report identifier and check count.</param>
    /// <param name="statusClient">External status API client.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sagaOptions">Saga settings.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Outgoing messages: a re-scheduled timeout when the status is still unknown, otherwise nothing.</returns>
    public static async Task<OutgoingMessages> Handle(
        ReportStatusCheckTimeout message,
        IReportStatusClient statusClient,
        AppDbContext dbContext,
        IOptions<SagaOptions> sagaOptions,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var usage = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (usage is null)
        {
            logger.LogDebug("Timeout check for unknown report {ReportId}, ignoring", message.ReportId);
            return [];
        }

        if (usage.Status != ReportUsageStatus.Pending)
        {
            logger.LogDebug("Timeout check for report {ReportId} in {Status}, ignoring", message.ReportId, usage.Status);
            return [];
        }

        var status = await statusClient.GetStatusAsync(message.ReportId, ct);
        var messages = new OutgoingMessages();

        switch (status)
        {
            case ReportStatus.Success:
                usage.Status = ReportUsageStatus.Charged;
                usage.FinishedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Report {ReportId} resolved as charged via timeout check", message.ReportId);
                return messages;

            case ReportStatus.Failed:
                usage.Status = ReportUsageStatus.NotCharged;
                usage.FinishedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Report {ReportId} resolved as not charged via timeout check", message.ReportId);
                return messages;

            case ReportStatus.Unknown:
            default:
                var nextCheck = message.CheckCount + 1;
                if (nextCheck >= sagaOptions.Value.MaxTimeoutRetries)
                {
                    usage.Status = ReportUsageStatus.NotCharged;
                    usage.FinishedAt = timeProvider.GetUtcNow();
                    await dbContext.SaveChangesAsync(ct);
                    logger.LogWarning(
                        "Report {ReportId}: status unknown after {Checks} checks, accounting as not charged",
                        message.ReportId,
                        nextCheck + 1);
                }
                else
                {
                    logger.LogInformation(
                        "Report {ReportId}: status unknown, check {Check}/{Max}, re-scheduling",
                        message.ReportId,
                        nextCheck + 1,
                        sagaOptions.Value.MaxTimeoutRetries);
                    messages.Schedule(
                        new ReportStatusCheckTimeout(message.ReportId, nextCheck),
                        timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
                }

                return messages;
        }
    }
}

using Microsoft.Extensions.Options;
using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;
using Wolverine;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Handles the <see cref="ReportOrdered"/> event: identifies the subject,
/// creates a <see cref="ReportUsage"/> in <see cref="ReportUsageStatus.Pending"/> status,
/// and schedules the first status check timeout.
/// Idempotent: a duplicate event for an already known report is ignored.
/// </summary>
public static class ReportOrderedHandler
{
    /// <summary>
    /// Processes the report ordered event.
    /// </summary>
    /// <param name="message">The report ordered event.</param>
    /// <param name="identification">Subject identification service.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sagaOptions">Saga settings (timeout, max retries).</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Outgoing messages containing the scheduled status check timeout.</returns>
    public static async Task<OutgoingMessages> Handle(
        ReportOrdered message,
        SubjectIdentificationService identification,
        AppDbContext dbContext,
        IOptions<SagaOptions> sagaOptions,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var existing = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (existing is not null)
        {
            logger.LogInformation("Duplicate ReportOrdered for report {ReportId}, ignoring", message.ReportId);
            return [];
        }

        var subjectId = await identification.IdentifyAsync(message.Subject, ct);
        dbContext.ReportUsages.Add(new ReportUsage
        {
            ReportId = message.ReportId,
            SubjectId = subjectId,
            IsFree = message.IsFree,
            Status = ReportUsageStatus.Pending,
            OrderedAt = message.OrderedAt,
        });
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Accounting started for report {ReportId}, subject {SubjectId}, isFree={IsFree}",
            message.ReportId,
            subjectId,
            message.IsFree);

        var messages = new OutgoingMessages();
        messages.Schedule(
            new ReportStatusCheckTimeout(message.ReportId, 0),
            timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
        return messages;
    }
}

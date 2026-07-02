using Microsoft.Extensions.Options;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Report accounting saga (technical spec, § 7.3): starts on <see cref="ReportOrdered"/>,
/// finishes on <see cref="ReportCompleted"/> (charged) or <see cref="ReportFailed"/> (not charged),
/// and resolves stalled reports through the external status API on scheduled timeouts.
/// </summary>
public class ReportAccountingSaga : Saga
{
    /// <summary>Saga identity — equals the report order identifier.</summary>
    [SagaIdentity]
    public Guid Id { get; set; }

    /// <summary>Internal identifier of the subject the report was ordered for.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Whether the report is free of charge.</summary>
    public bool IsFree { get; set; }

    /// <summary>Moment the report was ordered.</summary>
    public DateTimeOffset OrderedAt { get; set; }

    /// <summary>Number of status checks performed so far in the timeout branch.</summary>
    public int TimeoutCheckCount { get; set; }

    /// <summary>
    /// Starts the saga: identifies the subject, creates a <see cref="ReportUsage"/> record
    /// in the <see cref="ReportUsageStatus.Pending"/> status and schedules the first status check.
    /// Idempotent: a duplicate <see cref="ReportOrdered"/> for an already known report is ignored.
    /// </summary>
    /// <param name="message">The report ordered event.</param>
    /// <param name="identification">Subject identification service.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sagaOptions">Saga settings.</param>
    /// <param name="timeProvider">Time provider used for scheduling.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Outgoing messages containing the scheduled <see cref="ReportStatusCheckTimeout"/>.</returns>
    public async Task<OutgoingMessages> Start(
        ReportOrdered message,
        SubjectIdentificationService identification,
        AppDbContext dbContext,
        IOptions<SagaOptions> sagaOptions,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        Id = message.ReportId;
        IsFree = message.IsFree;
        OrderedAt = message.OrderedAt;

        var existing = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (existing is not null)
        {
            logger.LogInformation("Duplicate ReportOrdered for report {ReportId}, ignoring", message.ReportId);
            SubjectId = existing.SubjectId;
        }
        else
        {
            SubjectId = await identification.IdentifyAsync(message.Subject, ct);
            dbContext.ReportUsages.Add(new ReportUsage
            {
                ReportId = message.ReportId,
                SubjectId = SubjectId,
                IsFree = message.IsFree,
                Status = ReportUsageStatus.Pending,
                OrderedAt = message.OrderedAt,
            });
            logger.LogInformation(
                "Accounting saga started for report {ReportId}, subject {SubjectId}, isFree={IsFree}",
                message.ReportId,
                SubjectId,
                message.IsFree);
        }

        var messages = new OutgoingMessages();
        messages.Schedule(
            new ReportStatusCheckTimeout(message.ReportId),
            timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
        return messages;
    }

    /// <summary>
    /// Completes the saga: the overall report saga succeeded, the report is charged.
    /// </summary>
    /// <param name="message">The report completed event.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(
        ReportCompleted message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        await FinishAsync(dbContext, timeProvider, ReportUsageStatus.Charged, ct);
        logger.LogInformation("Report {ReportId} accounted as charged", message.ReportId);
        MarkCompleted();
    }

    /// <summary>
    /// Completes the saga: the overall report saga failed, the report is not charged.
    /// </summary>
    /// <param name="message">The report failed event.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(
        ReportFailed message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
        logger.LogInformation(
            "Report {ReportId} accounted as not charged (reason: {Reason})",
            message.ReportId,
            message.Reason ?? "-");
        MarkCompleted();
    }

    /// <summary>
    /// Timeout branch: queries the external status API and either finishes the saga
    /// (status resolved or retries exhausted) or schedules another check.
    /// </summary>
    /// <param name="message">The scheduled timeout message.</param>
    /// <param name="statusClient">External status API client.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sagaOptions">Saga settings.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Outgoing messages: a re-scheduled timeout when the status is still unknown, otherwise nothing.</returns>
    public async Task<OutgoingMessages> Handle(
        ReportStatusCheckTimeout message,
        IReportStatusClient statusClient,
        AppDbContext dbContext,
        IOptions<SagaOptions> sagaOptions,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        var messages = new OutgoingMessages();
        var status = await statusClient.GetStatusAsync(message.ReportId, ct);

        switch (status)
        {
            case ReportStatus.Success:
                await FinishAsync(dbContext, timeProvider, ReportUsageStatus.Charged, ct);
                logger.LogInformation("Report {ReportId} resolved as charged via status API", message.ReportId);
                MarkCompleted();
                break;

            case ReportStatus.Failed:
                await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
                logger.LogInformation("Report {ReportId} resolved as not charged via status API", message.ReportId);
                MarkCompleted();
                break;

            case ReportStatus.Unknown:
            default:
                TimeoutCheckCount++;
                if (TimeoutCheckCount >= sagaOptions.Value.MaxTimeoutRetries)
                {
                    await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
                    logger.LogWarning(
                        "Report {ReportId}: status unknown after {Checks} checks, accounting as not charged",
                        message.ReportId,
                        TimeoutCheckCount);
                    MarkCompleted();
                }
                else
                {
                    logger.LogInformation(
                        "Report {ReportId}: status unknown, check {Check}/{Max}, re-scheduling",
                        message.ReportId,
                        TimeoutCheckCount,
                        sagaOptions.Value.MaxTimeoutRetries);
                    messages.Schedule(
                        new ReportStatusCheckTimeout(message.ReportId),
                        timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
                }

                break;
        }

        return messages;
    }

    /// <summary>
    /// Handles a <see cref="ReportCompleted"/> event that arrived when no saga exists
    /// (race with <see cref="ReportOrdered"/> or a duplicate after completion) — Q2 decision: log and ack.
    /// </summary>
    /// <param name="message">The orphan event.</param>
    /// <param name="logger">Logger.</param>
    public static void NotFound(ReportCompleted message, ILogger<ReportAccountingSaga> logger)
        => logger.LogWarning("ReportCompleted for unknown saga {ReportId}, discarding", message.ReportId);

    /// <summary>
    /// Handles a <see cref="ReportFailed"/> event that arrived when no saga exists — Q2 decision: log and ack.
    /// </summary>
    /// <param name="message">The orphan event.</param>
    /// <param name="logger">Logger.</param>
    public static void NotFound(ReportFailed message, ILogger<ReportAccountingSaga> logger)
        => logger.LogWarning("ReportFailed for unknown saga {ReportId}, discarding", message.ReportId);

    /// <summary>
    /// Handles a timeout message whose saga has already completed. No action required.
    /// </summary>
    /// <param name="message">The orphan timeout message.</param>
    /// <param name="logger">Logger.</param>
    public static void NotFound(ReportStatusCheckTimeout message, ILogger<ReportAccountingSaga> logger)
        => logger.LogDebug("Timeout for already completed saga {ReportId}, ignoring", message.ReportId);

    private async Task FinishAsync(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ReportUsageStatus finalStatus,
        CancellationToken ct)
    {
        var usage = await dbContext.ReportUsages.FindAsync([Id], ct)
            ?? throw new InvalidOperationException($"ReportUsage record for report {Id} not found.");
        if (usage.Status == ReportUsageStatus.Pending)
        {
            usage.Status = finalStatus;
            usage.FinishedAt = timeProvider.GetUtcNow();
        }
    }
}

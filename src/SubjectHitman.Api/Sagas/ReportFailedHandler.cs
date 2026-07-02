using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Обрабатывает событие <see cref="ReportFailed"/>: помечает отчёт как неоплаченный.
/// Если отчёт неизвестен, событие логируется и отбрасывается (Q2).
/// </summary>
public static class ReportFailedHandler
{
    /// <summary>
    /// Обрабатывает событие ошибки отчёта.
    /// </summary>
    /// <param name="message">Событие ошибки отчёта.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
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

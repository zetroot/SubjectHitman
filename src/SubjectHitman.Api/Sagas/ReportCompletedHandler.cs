using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Обрабатывает событие <see cref="ReportCompleted"/>: помечает отчёт как оплаченный.
/// Если отчёт неизвестен (состояние гонки с <see cref="ReportOrdered"/>), событие логируется и отбрасывается (Q2).
/// </summary>
public static class ReportCompletedHandler
{
    /// <summary>
    /// Обрабатывает событие завершения отчёта.
    /// </summary>
    /// <param name="message">Событие завершения отчёта.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
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

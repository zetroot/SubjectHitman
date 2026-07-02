using Microsoft.Extensions.Options;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;
using Wolverine;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Запланированный обработчик, который проверяет статус ожидающего отчёта через внешний API статусов
/// и либо завершает учёт отчёта, либо перепланирует следующую проверку (до <c>MaxTimeoutRetries</c> раз).
/// </summary>
public static class ReportStatusCheckTimeoutHandler
{
    /// <summary>
    /// Обрабатывает запланированный таймаут: запрашивает внешний API статусов и действует по результату.
    /// </summary>
    /// <param name="message">Сообщение таймаута с идентификатором отчёта и счётчиком проверок.</param>
    /// <param name="statusClient">Клиент внешнего API статусов.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="sagaOptions">Настройки саги.</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Исходящие сообщения: перепланированный таймаут, если статус всё ещё неизвестен, иначе ничего.</returns>
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

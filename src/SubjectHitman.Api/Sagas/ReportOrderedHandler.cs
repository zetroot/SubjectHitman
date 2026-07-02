using Microsoft.Extensions.Options;
using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Api.Domain;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;
using Wolverine;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Обрабатывает событие <see cref="ReportOrdered"/>: определяет субъект,
/// создаёт запись <see cref="ReportUsage"/> в статусе <see cref="ReportUsageStatus.Pending"/>,
/// и планирует первую проверку статуса по таймауту.
/// Идемпотентен: повторное событие для уже известного отчёта игнорируется.
/// </summary>
public static class ReportOrderedHandler
{
    /// <summary>
    /// Обрабатывает событие заказа отчёта.
    /// </summary>
    /// <param name="message">Событие заказа отчёта.</param>
    /// <param name="identification">Сервис идентификации субъекта.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="sagaOptions">Настройки саги (таймаут, максимальное количество повторов).</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Исходящие сообщения, содержащие запланированную проверку статуса по таймауту.</returns>
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

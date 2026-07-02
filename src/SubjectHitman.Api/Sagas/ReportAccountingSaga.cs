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
/// Учётная сага отчёта (техническая спецификация, § 7.3): стартует по <see cref="ReportOrdered"/>,
/// завершается по <see cref="ReportCompleted"/> (списан) или <see cref="ReportFailed"/> (не списан),
/// разруливает зависшие отчёты через внешний статусный API по scheduled timeout.
/// </summary>
public class ReportAccountingSaga : Saga
{
    /// <summary>
    /// Идентичность саги — равна идентификатору заказа отчёта.
    /// Название <c>ReportId</c> совпадает с одноимёнными свойствами в сообщениях — Wolverine авто-коррелирует.
    /// </summary>
    [SagaIdentity]
    public Guid ReportId { get; set; }

    /// <summary>Внутренний идентификатор субъекта, для которого заказан отчёт.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Признак бесплатности отчёта.</summary>
    public bool IsFree { get; set; }

    /// <summary>Момент заказа отчёта.</summary>
    public DateTimeOffset OrderedAt { get; set; }

    /// <summary>Количество выполненных проверок статуса в timeout-ветке.</summary>
    public int TimeoutCheckCount { get; set; }

    /// <summary>
    /// Стартует сагу: идентифицирует субъекта, создаёт запись <see cref="ReportUsage"/>
    /// в статусе <see cref="ReportUsageStatus.Pending"/> и планирует первую проверку статуса.
    /// Идемпотентна: повторный <see cref="ReportOrdered"/> для уже известного отчёта игнорируется.
    /// </summary>
    /// <param name="message">Событие заказа отчёта.</param>
    /// <param name="identification">Сервис идентификации субъекта.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="sagaOptions">Настройки саги.</param>
    /// <param name="timeProvider">Провайдер времени для планирования timeout.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Исходящие сообщения с запланированным <see cref="ReportStatusCheckTimeout"/>.</returns>
    public async Task<OutgoingMessages> Start(
        [SagaIdentityFrom("ReportId")] ReportOrdered message,
        SubjectIdentificationService identification,
        AppDbContext dbContext,
        IOptions<SagaOptions> sagaOptions,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        ReportId = message.ReportId;
        IsFree = message.IsFree;
        OrderedAt = message.OrderedAt;

        var existing = await dbContext.ReportUsages.FindAsync([message.ReportId], ct);
        if (existing is not null)
        {
            logger.LogInformation("Повторный ReportOrdered для отчёта {ReportId}, игнорируется", message.ReportId);
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
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Сага учёта запущена для отчёта {ReportId}, субъект {SubjectId}, isFree={IsFree}",
                message.ReportId,
                SubjectId,
                message.IsFree);
        }

        var messages = new OutgoingMessages();
        messages.Schedule(
            new ReportStatusCheckTimeout(message.ReportId, 0),
            timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
        return messages;
    }

    /// <summary>
    /// Завершает сагу: общая сага обработки отчёта завершилась успешно, отчёт списан.
    /// </summary>
    /// <param name="message">Событие успешного завершения.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task Handle(
        [SagaIdentityFrom("ReportId")] ReportCompleted message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        await FinishAsync(dbContext, timeProvider, ReportUsageStatus.Charged, ct);
        logger.LogInformation("Отчёт {ReportId} учтён как списанный", message.ReportId);
        MarkCompleted();
    }

    /// <summary>
    /// Завершает сагу: общая сага обработки отчёта завершилась неуспешно, отчёт не списан.
    /// </summary>
    /// <param name="message">Событие неуспешного завершения.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="timeProvider">Провайдер времени.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task Handle(
        [SagaIdentityFrom("ReportId")] ReportFailed message,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ReportAccountingSaga> logger,
        CancellationToken ct)
    {
        await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
        logger.LogInformation(
            "Отчёт {ReportId} учтён как не списанный (причина: {Reason})",
            message.ReportId,
            message.Reason ?? "-");
        MarkCompleted();
    }

    /// <summary>
    /// Timeout-ветка: запрашивает статус отчёта через внешнее API и либо завершает сагу
    /// (статус определён или ретраи исчерпаны), либо перепланирует следующую проверку.
    /// </summary>
    /// <param name="message">Запланированное timeout-сообщение.</param>
    /// <param name="statusClient">Клиент внешнего статусного API.</param>
    /// <param name="dbContext">Контекст базы данных.</param>
    /// <param name="sagaOptions">Настройки саги.</param>
    /// <param name="timeProvider">Провайдер времени для перепланирования.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Исходящие сообщения: перепланированный timeout при Unknown-статусе, иначе пусто.</returns>
    public async Task<OutgoingMessages> Handle(
        [SagaIdentityFrom("ReportId")] ReportStatusCheckTimeout message,
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
                logger.LogInformation("Отчёт {ReportId} разрешён как списанный через статусное API", message.ReportId);
                MarkCompleted();
                break;

            case ReportStatus.Failed:
                await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
                logger.LogInformation("Отчёт {ReportId} разрешён как не списанный через статусное API", message.ReportId);
                MarkCompleted();
                break;

            case ReportStatus.Unknown:
            default:
                TimeoutCheckCount++;
                if (TimeoutCheckCount >= sagaOptions.Value.MaxTimeoutRetries)
                {
                    await FinishAsync(dbContext, timeProvider, ReportUsageStatus.NotCharged, ct);
                    logger.LogWarning(
                        "Отчёт {ReportId}: статус неизвестен после {Checks} проверок, учитывается как не списанный",
                        message.ReportId,
                        TimeoutCheckCount);
                    MarkCompleted();
                }
                else
                {
                    logger.LogInformation(
                        "Отчёт {ReportId}: статус неизвестен, проверка {Check}/{Max}, перепланирование",
                        message.ReportId,
                        TimeoutCheckCount,
                        sagaOptions.Value.MaxTimeoutRetries);
                    messages.Schedule(
                        new ReportStatusCheckTimeout(message.ReportId, TimeoutCheckCount),
                        timeProvider.GetUtcNow() + sagaOptions.Value.Timeout);
                }

                break;
        }

        return messages;
    }

    /// <summary>
    /// Обрабатывает <see cref="ReportCompleted"/> для неизвестной саги
    /// (гонка с <see cref="ReportOrdered"/> или дубликат после завершения) — Q2: логировать и подтвердить.
    /// </summary>
    /// <param name="message">Осиротевшее событие.</param>
    /// <param name="logger">Логгер.</param>
    public static void NotFound(ReportCompleted message, ILogger<ReportAccountingSaga> logger)
        => logger.LogWarning("ReportCompleted для неизвестной саги {ReportId}, отбрасывается", message.ReportId);

    /// <summary>
    /// Обрабатывает <see cref="ReportFailed"/> для неизвестной саги — Q2: логировать и подтвердить.
    /// </summary>
    /// <param name="message">Осиротевшее событие.</param>
    /// <param name="logger">Логгер.</param>
    public static void NotFound(ReportFailed message, ILogger<ReportAccountingSaga> logger)
        => logger.LogWarning("ReportFailed для неизвестной саги {ReportId}, отбрасывается", message.ReportId);

    /// <summary>
    /// Обрабатывает timeout для уже завершённой саги. Действий не требуется.
    /// </summary>
    /// <param name="message">Осиротевшее timeout-сообщение.</param>
    /// <param name="logger">Логгер.</param>
    public static void NotFound(ReportStatusCheckTimeout message, ILogger<ReportAccountingSaga> logger)
        => logger.LogDebug("Timeout для уже завершённой саги {ReportId}, игнорируется", message.ReportId);

    private async Task FinishAsync(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ReportUsageStatus finalStatus,
        CancellationToken ct)
    {
        var usage = await dbContext.ReportUsages.FindAsync([ReportId], ct)
            ?? throw new InvalidOperationException($"Запись учёта для отчёта {ReportId} не найдена.");
        if (usage.Status == ReportUsageStatus.Pending)
        {
            usage.Status = finalStatus;
            usage.FinishedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(ct);
        }
    }
}

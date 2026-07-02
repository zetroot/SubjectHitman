namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Событие, публикуемое оркестратором обработки отчётов при заказе кредитного отчёта.
/// Запускает процесс учёта отчёта в этом компоненте.
/// </summary>
/// <param name="ReportId">Уникальный идентификатор заказа отчёта.</param>
/// <param name="OrderedAt">Момент заказа отчёта. Используется для отнесения отчёта к календарному году.</param>
/// <param name="IsFree">Признак того, что заказанный отчёт бесплатный для субъекта.</param>
/// <param name="Subject">Персональные данные субъекта, для которого заказан отчёт.</param>
public record ReportOrdered(Guid ReportId, DateTimeOffset OrderedAt, bool IsFree, SubjectData Subject);

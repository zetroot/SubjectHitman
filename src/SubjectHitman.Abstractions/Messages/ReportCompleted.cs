namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Событие, публикуемое оркестратором обработки отчётов при успешном завершении общей саги
/// и оповещении клиента о готовности отчёта. Отчёт должен быть учтён как списанный.
/// </summary>
/// <param name="ReportId">Уникальный идентификатор заказа отчёта.</param>
/// <param name="CompletedAt">Момент завершения общей саги.</param>
public record ReportCompleted(Guid ReportId, DateTimeOffset CompletedAt);

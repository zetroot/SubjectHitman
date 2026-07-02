namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Событие, публикуемое оркестратором обработки отчётов при неуспешном завершении общей саги,
/// когда отчёт не может быть изготовлен. Отчёт должен быть учтён как не списанный.
/// </summary>
/// <param name="ReportId">Уникальный идентификатор заказа отчёта.</param>
/// <param name="FailedAt">Момент неуспешного завершения общей саги.</param>
/// <param name="Reason">Необязательная человекочитаемая причина неуспеха.</param>
public record ReportFailed(Guid ReportId, DateTimeOffset FailedAt, string? Reason);

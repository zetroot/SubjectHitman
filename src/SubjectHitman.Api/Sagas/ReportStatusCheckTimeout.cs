namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Внутреннее запланированное сообщение, которое запускает проверку статуса ожидающей саги учёта отчёта.
/// Никогда не покидает локальную устойчивую очередь.
/// </summary>
/// <param name="ReportId">Идентификатор заказа отчёта.</param>
/// <param name="CheckCount">Количество уже выполненных проверок статуса (начиная с 0).</param>
public record ReportStatusCheckTimeout(Guid ReportId, int CheckCount);

namespace SubjectHitman.Abstractions;

/// <summary>
/// Клиент внешнего API статуса основной системы обработки отчётов.
/// Используется в timeout-ветке учёта для определения финального состояния зависшего отчёта.
/// </summary>
public interface IReportStatusClient
{
    /// <summary>
    /// Возвращает текущий статус отчёта в основной системе.
    /// Реализации не должны бросать исключения при ошибках транспорта — любая ошибка
    /// возвращается как <see cref="ReportStatus.Unknown"/>.
    /// </summary>
    /// <param name="reportId">Идентификатор заказа отчёта.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Статус отчёта или <see cref="ReportStatus.Unknown"/>, если его не удалось определить.</returns>
    Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct);
}

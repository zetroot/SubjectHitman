namespace SubjectHitman.Api.Telemetry;

/// <summary>
/// Контракт публикатора метрик прикладного уровня (саги, статус-API).
/// </summary>
public interface IApiMetricsPublisher
{
    /// <summary>Регистрирует старт саги (<c>ReportOrdered</c> получен).</summary>
    void SagaStarted();

    /// <summary>Регистрирует получение дубликата <c>ReportOrdered</c> (идемпотентность).</summary>
    void SagaDuplicateOrder();

    /// <summary>Регистрирует завершение саги.</summary>
    /// <param name="status"><c>charged</c> или <c>not_charged</c>.</param>
    /// <param name="via">Способ завершения: <c>event</c>, <c>status_api</c> или <c>retries_exhausted</c>.</param>
    void SagaFinished(string status, string via);

    /// <summary>Регистрирует повторную проверку статуса по таймауту.</summary>
    void SagaTimeoutRecheck();

    /// <summary>Регистрирует осиротевшее событие саги (NotFound).</summary>
    /// <param name="eventType">Тип события: <c>ReportCompleted</c>, <c>ReportFailed</c>.</param>
    void SagaOrphanedEvent(string eventType);

    /// <summary>Регистрирует результат запроса к статус-API.</summary>
    /// <param name="result"><c>success</c>, <c>failed</c> или <c>unknown</c>.</param>
    void ReportStatusRequestCompleted(string result);

    /// <summary>Начинает измерение длительности запроса к статус-API.</summary>
    IDisposable MeasureReportStatusRequestDuration();
}

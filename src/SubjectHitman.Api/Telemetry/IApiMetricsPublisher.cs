namespace SubjectHitman.Api.Telemetry;

/// <summary>
/// Контракт публикатора метрик прикладного уровня (саги, идентификация, статус-API).
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

    /// <summary>Регистрирует завершение идентификации субъекта.</summary>
    /// <param name="outcome"><c>created</c> или <c>matched</c>.</param>
    void IdentificationCompleted(string outcome);

    /// <summary>Регистрирует разрешение конфликта при >1 кандидате на идентификацию.</summary>
    void IdentificationConflictResolved();

    /// <summary>Регистрирует конфликт скалярного ПДн при merge.</summary>
    /// <param name="field">Поле: <c>birth_date</c>, <c>inn</c> или <c>snils</c>.</param>
    void IdentificationPdConflict(string field);

    /// <summary>Начинает измерение длительности идентификации и возвращает токен для завершения замера.</summary>
    IDisposable MeasureIdentificationDuration();

    /// <summary>Регистрирует результат запроса к статус-API.</summary>
    /// <param name="result"><c>success</c>, <c>failed</c> или <c>unknown</c>.</param>
    void ReportStatusRequestCompleted(string result);

    /// <summary>Начинает измерение длительности запроса к статус-API.</summary>
    IDisposable MeasureReportStatusRequestDuration();
}

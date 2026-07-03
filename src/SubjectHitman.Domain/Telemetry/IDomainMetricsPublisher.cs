namespace SubjectHitman.Domain.Telemetry;

/// <summary>
/// Контракт публикатора метрик доменного уровня (идентификация субъекта).
/// Реализация находится в слое Api, публикующем метрики в Meter <c>SubjectHitman.Api</c>.
/// </summary>
public interface IDomainMetricsPublisher
{
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
}

namespace SubjectHitman.DataAccess.Telemetry;

/// <summary>
/// Контракт публикатора метрик для слоя доступа к данным.
/// </summary>
public interface IDataAccessMetricsPublisher
{
    /// <summary>Регистрирует ретрай после UniqueViolation при идентификации субъекта.</summary>
    void SubjectIdentificationRetry();

    /// <summary>Начинает измерение длительности транзакции идентификации и возвращает токен для завершения замера.</summary>
    IDisposable MeasureTransactionDuration();

    /// <summary>Регистрирует установку advisory-блокировок.</summary>
    /// <param name="lockCount">Количество установленных блокировок.</param>
    void AdvisoryLocksAcquired(int lockCount);
}

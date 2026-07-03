using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace SubjectHitman.DataAccess.Telemetry;

/// <summary>
/// Публикатор метрик слоя доступа к данным.
/// Создаёт <see cref="Meter"/> с именем <c>SubjectHitman.DataAccess</c> через фабрику из DI.
/// </summary>
public sealed class DataAccessMetricsPublisher : IDataAccessMetricsPublisher, IDisposable
{
    private readonly Counter<long> _uniqueViolationRetries;
    private readonly Counter<long> _advisoryLocksAcquired;
    private readonly Histogram<double> _transactionDuration;

    /// <summary>
    /// Инициализирует публикатор и регистрирует все инструменты метрик.
    /// </summary>
    /// <param name="meterFactory">Фабрика Meter из DI.</param>
    public DataAccessMetricsPublisher(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("SubjectHitman.DataAccess");

        _uniqueViolationRetries = meter.CreateCounter<long>(
            "subject_repository.unique_violation_retries",
            "retries",
            "Количество ретраев идентификации после UniqueViolation");

        _advisoryLocksAcquired = meter.CreateCounter<long>(
            "subject_repository.advisory_locks.acquired",
            "locks",
            "Количество установленных advisory-блокировок PostgreSQL");

        _transactionDuration = meter.CreateHistogram<double>(
            "subject_repository.transaction.duration",
            "ms",
            "Длительность транзакции идентификации субъекта в миллисекундах");
    }

    /// <inheritdoc />
    public void SubjectIdentificationRetry()
    {
        _uniqueViolationRetries.Add(1);
    }

    /// <inheritdoc />
    public IDisposable MeasureTransactionDuration()
    {
        var start = Stopwatch.GetTimestamp();
        return new DurationTracker(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _transactionDuration.Record(elapsed);
        });
    }

    /// <inheritdoc />
    public void AdvisoryLocksAcquired(int lockCount)
    {
        _advisoryLocksAcquired.Add(lockCount);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class DurationTracker(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

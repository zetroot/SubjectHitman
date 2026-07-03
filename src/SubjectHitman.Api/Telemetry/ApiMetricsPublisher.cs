using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace SubjectHitman.Api.Telemetry;

/// <summary>
/// Публикатор метрик прикладного уровня.
/// Создаёт <see cref="Meter"/> с именем <c>SubjectHitman.Api</c> через фабрику из DI.
/// </summary>
public sealed class ApiMetricsPublisher : IApiMetricsPublisher, IDisposable
{
    private readonly Counter<long> _sagaStarted;
    private readonly Counter<long> _sagaDuplicateOrders;
    private readonly Counter<long> _sagaFinished;
    private readonly Counter<long> _sagaTimeoutRechecks;
    private readonly Counter<long> _sagaOrphanedEvents;
    private readonly Counter<long> _identificationCompleted;
    private readonly Counter<long> _identificationConflictsResolved;
    private readonly Counter<long> _identificationPdConflicts;
    private readonly Histogram<double> _identificationDuration;
    private readonly Counter<long> _reportStatusRequests;
    private readonly Histogram<double> _reportStatusDuration;

    /// <summary>
    /// Инициализирует публикатор и регистрирует все инструменты метрик.
    /// </summary>
    /// <param name="meterFactory">Фабрика Meter из DI.</param>
    public ApiMetricsPublisher(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("SubjectHitman.Api");

        _sagaStarted = meter.CreateCounter<long>(
            "saga.started", "events", "Количество стартов саги");

        _sagaDuplicateOrders = meter.CreateCounter<long>(
            "saga.duplicate_orders", "events", "Количество дубликатов ReportOrdered");

        _sagaFinished = meter.CreateCounter<long>(
            "saga.finished", "events", "Количество завершённых саг");

        _sagaTimeoutRechecks = meter.CreateCounter<long>(
            "saga.timeout_rechecks", "events", "Количество повторных проверок статуса по таймауту");

        _sagaOrphanedEvents = meter.CreateCounter<long>(
            "saga.orphaned_events", "events", "Количество осиротевших событий саги");

        _identificationCompleted = meter.CreateCounter<long>(
            "identification.completed", "identifications", "Количество завершённых идентификаций");

        _identificationConflictsResolved = meter.CreateCounter<long>(
            "identification.conflicts_resolved", "conflicts", "Количество разрешённых конфликтов при идентификации");

        _identificationPdConflicts = meter.CreateCounter<long>(
            "identification.pd_conflicts", "conflicts", "Количество конфликтов скалярных ПДн при merge");

        _identificationDuration = meter.CreateHistogram<double>(
            "identification.duration", "ms", "Длительность идентификации субъекта в миллисекундах");

        _reportStatusRequests = meter.CreateCounter<long>(
            "report_status.requests", "requests", "Количество запросов к статус-API");

        _reportStatusDuration = meter.CreateHistogram<double>(
            "report_status.duration", "ms", "Длительность запроса к статус-API в миллисекундах");
    }

    /// <inheritdoc />
    public void SagaStarted() => _sagaStarted.Add(1);

    /// <inheritdoc />
    public void SagaDuplicateOrder() => _sagaDuplicateOrders.Add(1);

    /// <inheritdoc />
    public void SagaFinished(string status, string via)
    {
        var tag = new TagList
        {
            { "status", status },
            { "via", via },
        };
        _sagaFinished.Add(1, tag);
    }

    /// <inheritdoc />
    public void SagaTimeoutRecheck() => _sagaTimeoutRechecks.Add(1);

    /// <inheritdoc />
    public void SagaOrphanedEvent(string eventType)
    {
        var tag = new TagList { { "event_type", eventType } };
        _sagaOrphanedEvents.Add(1, tag);
    }

    /// <inheritdoc />
    public void IdentificationCompleted(string outcome)
    {
        var tag = new TagList { { "outcome", outcome } };
        _identificationCompleted.Add(1, tag);
    }

    /// <inheritdoc />
    public void IdentificationConflictResolved() => _identificationConflictsResolved.Add(1);

    /// <inheritdoc />
    public void IdentificationPdConflict(string field)
    {
        var tag = new TagList { { "field", field } };
        _identificationPdConflicts.Add(1, tag);
    }

    /// <inheritdoc />
    public IDisposable MeasureIdentificationDuration()
    {
        var start = Stopwatch.GetTimestamp();
        return new DurationTracker(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _identificationDuration.Record(elapsed);
        });
    }

    /// <inheritdoc />
    public void ReportStatusRequestCompleted(string result)
    {
        var tag = new TagList { { "result", result } };
        _reportStatusRequests.Add(1, tag);
    }

    /// <inheritdoc />
    public IDisposable MeasureReportStatusRequestDuration()
    {
        var start = Stopwatch.GetTimestamp();
        return new DurationTracker(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _reportStatusDuration.Record(elapsed);
        });
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

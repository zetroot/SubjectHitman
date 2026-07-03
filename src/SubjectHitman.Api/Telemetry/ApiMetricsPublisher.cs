using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;
using SubjectHitman.Domain.Telemetry;

namespace SubjectHitman.Api.Telemetry;

/// <summary>
/// Публикатор метрик прикладного уровня.
/// Создаёт <see cref="Meter"/> с именем <c>SubjectHitman.Api</c> через фабрику из DI.
/// Реализует как <see cref="IApiMetricsPublisher"/> (саги, статус-API), так и <see cref="IDomainMetricsPublisher"/> (идентификация).
/// </summary>
public sealed class ApiMetricsPublisher : IApiMetricsPublisher, IDomainMetricsPublisher, IDisposable
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
            "saga.started", "events", "Number of saga starts");

        _sagaDuplicateOrders = meter.CreateCounter<long>(
            "saga.duplicate_orders", "events", "Number of duplicate ReportOrdered received");

        _sagaFinished = meter.CreateCounter<long>(
            "saga.finished", "events", "Number of finished sagas");

        _sagaTimeoutRechecks = meter.CreateCounter<long>(
            "saga.timeout_rechecks", "events", "Number of timeout status rechecks");

        _sagaOrphanedEvents = meter.CreateCounter<long>(
            "saga.orphaned_events", "events", "Number of orphaned saga events");

        _identificationCompleted = meter.CreateCounter<long>(
            "identification.completed", "identifications", "Number of completed identifications");

        _identificationConflictsResolved = meter.CreateCounter<long>(
            "identification.conflicts_resolved", "conflicts", "Number of resolved identification conflicts");

        _identificationPdConflicts = meter.CreateCounter<long>(
            "identification.pd_conflicts", "conflicts", "Number of scalar personal data conflicts during merge");

        _identificationDuration = meter.CreateHistogram<double>(
            "identification.duration", "ms", "Subject identification duration in milliseconds");

        _reportStatusRequests = meter.CreateCounter<long>(
            "report_status.requests", "requests", "Number of status API requests");

        _reportStatusDuration = meter.CreateHistogram<double>(
            "report_status.duration", "ms", "Status API request duration in milliseconds");
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

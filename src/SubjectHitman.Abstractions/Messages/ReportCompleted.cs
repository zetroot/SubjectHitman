namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Event published by the report-processing orchestrator when the overall report saga finished successfully
/// and the client was notified that the report is ready. The report must be accounted as charged.
/// </summary>
/// <param name="ReportId">Unique identifier of the report order.</param>
/// <param name="CompletedAt">Moment the overall saga completed.</param>
public record ReportCompleted(Guid ReportId, DateTimeOffset CompletedAt);

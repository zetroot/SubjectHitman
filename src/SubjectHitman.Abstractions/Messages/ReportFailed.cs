namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Event published by the report-processing orchestrator when the overall report saga failed
/// and the report cannot be produced. The report must be accounted as not charged.
/// </summary>
/// <param name="ReportId">Unique identifier of the report order.</param>
/// <param name="FailedAt">Moment the overall saga failed.</param>
/// <param name="Reason">Optional human-readable failure reason.</param>
public record ReportFailed(Guid ReportId, DateTimeOffset FailedAt, string? Reason);

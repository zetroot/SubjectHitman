namespace SubjectHitman.Abstractions.Messages;

/// <summary>
/// Event published by the report-processing orchestrator when a credit report has been ordered.
/// Starts the report accounting saga in this component.
/// </summary>
/// <param name="ReportId">Unique identifier of the report order.</param>
/// <param name="OrderedAt">Moment the report was ordered. Used to attribute the report to a calendar year.</param>
/// <param name="IsFree">Whether the ordered report is free of charge for the subject.</param>
/// <param name="Subject">Personal data of the subject the report was ordered for.</param>
public record ReportOrdered(Guid ReportId, DateTimeOffset OrderedAt, bool IsFree, SubjectData Subject);

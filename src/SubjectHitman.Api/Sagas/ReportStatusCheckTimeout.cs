namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Internal scheduled message that triggers a status check of a pending report accounting saga.
/// Never leaves the local durable queue.
/// </summary>
/// <param name="ReportId">Report order identifier (saga identity).</param>
public record ReportStatusCheckTimeout(Guid ReportId);

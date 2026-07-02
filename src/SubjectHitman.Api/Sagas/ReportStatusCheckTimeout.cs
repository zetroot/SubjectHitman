namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Internal scheduled message that triggers a status check of a pending report accounting saga.
/// Never leaves the local durable queue.
/// </summary>
/// <param name="ReportId">Report order identifier.</param>
/// <param name="CheckCount">Number of status checks already performed (0-based).</param>
public record ReportStatusCheckTimeout(Guid ReportId, int CheckCount);

namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Response body of the external status API endpoint <c>GET /reports/{reportId}/status</c>.
/// </summary>
/// <param name="ReportId">Report order identifier.</param>
/// <param name="Status">Current status of the report in the main system.</param>
public record ReportStatusResponse(Guid ReportId, ReportStatus Status);

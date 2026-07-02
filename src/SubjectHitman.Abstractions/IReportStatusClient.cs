namespace SubjectHitman.Abstractions;

/// <summary>
/// Client for the external report-processing system's status API.
/// Used by the accounting saga timeout branch to resolve the final state of a stalled report.
/// </summary>
public interface IReportStatusClient
{
    /// <summary>
    /// Gets the current status of a report in the main system.
    /// Implementations must not throw on transport failures — any error is reported
    /// as <see cref="ReportStatus.Unknown"/>.
    /// </summary>
    /// <param name="reportId">Report order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The report status, or <see cref="ReportStatus.Unknown"/> when it cannot be determined.</returns>
    Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct);
}

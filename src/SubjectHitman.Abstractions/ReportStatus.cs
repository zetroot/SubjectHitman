namespace SubjectHitman.Abstractions;

/// <summary>
/// Status of a report in the main report-processing system,
/// as returned by the external status API (queried in the saga timeout branch).
/// </summary>
public enum ReportStatus
{
    /// <summary>The status could not be determined (also used for transport errors and timeouts).</summary>
    Unknown = 0,

    /// <summary>The report was produced successfully and delivered to the client.</summary>
    Success = 1,

    /// <summary>The report production failed.</summary>
    Failed = 2,
}

namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Accounting status of an ordered report.
/// </summary>
public enum ReportUsageStatus : short
{
    /// <summary>The report is ordered; the overall processing saga has not finished yet.</summary>
    Pending = 0,

    /// <summary>The report was produced and delivered; it counts against the subject's free quota.</summary>
    Charged = 1,

    /// <summary>The report was not produced; it does not count against the quota.</summary>
    NotCharged = 2,
}

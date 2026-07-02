using System.ComponentModel.DataAnnotations;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Settings of the report accounting saga.
/// </summary>
public class SagaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Saga";

    /// <summary>
    /// Interval after which a pending saga checks the report status in the main system.
    /// Also the interval between consecutive checks.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of status checks. When exceeded and the status is still unknown,
    /// the report is accounted as not charged and the saga completes.
    /// </summary>
    [Range(1, 1000)]
    public int MaxTimeoutRetries { get; set; } = 5;
}

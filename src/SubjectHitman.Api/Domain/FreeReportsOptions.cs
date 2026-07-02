using System.ComponentModel.DataAnnotations;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Settings of the free report counting logic.
/// </summary>
public class FreeReportsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FreeReports";

    /// <summary>
    /// Cooldown period: free reports charged within this interval from the first report of a group
    /// are counted as a single report (duplicate-order protection).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// IANA identifier of the time zone in which calendar-year boundaries are calculated.
    /// </summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Moscow";
}

namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Accounting record of a single ordered credit report.
/// The <see cref="ReportId"/> primary key doubles as the idempotency key for saga events.
/// </summary>
public class ReportUsage
{
    /// <summary>Report order identifier (from the <c>ReportOrdered</c> event).</summary>
    public Guid ReportId { get; set; }

    /// <summary>Identifier of the subject the report was ordered for.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Whether the report is free of charge for the subject.</summary>
    public bool IsFree { get; set; }

    /// <summary>Current accounting status of the report.</summary>
    public ReportUsageStatus Status { get; set; }

    /// <summary>Moment the report was ordered. Basis for calendar-year attribution and cooldown grouping.</summary>
    public DateTimeOffset OrderedAt { get; set; }

    /// <summary>Moment the accounting saga finished (charged or not charged); <see langword="null"/> while pending.</summary>
    public DateTimeOffset? FinishedAt { get; set; }
}

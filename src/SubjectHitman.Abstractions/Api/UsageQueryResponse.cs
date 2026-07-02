namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Response body of <c>POST /api/v1/free-reports/usage-query</c>.
/// </summary>
/// <param name="SubjectId">Internal identifier of the identified (or newly created) subject.</param>
/// <param name="UsedFreeReportsCount">
/// Number of free reports charged to the subject within the current calendar year,
/// with reports inside the configured cooldown period collapsed into one.
/// </param>
/// <param name="PeriodStart">Inclusive start of the counted period (January 1 of the current year in the configured time zone).</param>
/// <param name="PeriodEnd">Inclusive end of the counted period (December 31, 23:59:59 of the current year in the configured time zone).</param>
public record UsageQueryResponse(
    Guid SubjectId,
    int UsedFreeReportsCount,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

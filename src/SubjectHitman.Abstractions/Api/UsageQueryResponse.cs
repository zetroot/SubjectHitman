namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Тело ответа <c>POST /api/v1/free-reports/usage-query</c>.
/// </summary>
/// <param name="SubjectId">Внутренний идентификатор идентифицированного (или вновь созданного) субъекта.</param>
/// <param name="UsedFreeReportsCount">
/// Количество бесплатных отчётов, списанных субъекту в текущем календарном году,
/// с учётом схлопывания отчётов внутри настроенного cooldown-периода в одну группу.
/// </param>
/// <param name="PeriodStart">Включающая нижняя граница учитываемого периода (1 января текущего года в настроенной таймзоне).</param>
/// <param name="PeriodEnd">Включающая верхняя граница учитываемого периода (31 декабря 23:59:59 текущего года в настроенной таймзоне).</param>
public record UsageQueryResponse(
    Guid SubjectId,
    int UsedFreeReportsCount,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

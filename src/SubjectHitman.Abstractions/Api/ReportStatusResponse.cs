namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Тело ответа внешнего статусного API <c>GET /reports/{reportId}/status</c>.
/// </summary>
/// <param name="ReportId">Идентификатор заказа отчёта.</param>
/// <param name="Status">Текущий статус отчёта в основной системе.</param>
public record ReportStatusResponse(Guid ReportId, ReportStatus Status);

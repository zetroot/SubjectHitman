using System.Collections.Concurrent;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();

var statuses = new ConcurrentDictionary<Guid, ReportStatus>();

app.MapGet(
    "/reports/{reportId:guid}/status",
    (Guid reportId) =>
    {
        var status = statuses.GetValueOrDefault(reportId, ReportStatus.Unknown);
        return Results.Ok(new ReportStatusResponse(reportId, status));
    });

app.MapPut(
    "/reports/{reportId:guid}/status",
    (Guid reportId, SetStatusRequest request) =>
    {
        statuses[reportId] = request.Status;
        return Results.NoContent();
    });

app.MapDelete(
    "/reports",
    () =>
    {
        statuses.Clear();
        return Results.NoContent();
    });

app.MapHealthChecks("/health");
app.Run();

/// <summary>
/// Тело запроса управляющего эндпоинта мока <c>PUT /reports/{reportId}/status</c>.
/// </summary>
/// <param name="Status">Статус, который мок должен возвращать для указанного отчёта.</param>
public record SetStatusRequest(ReportStatus Status);

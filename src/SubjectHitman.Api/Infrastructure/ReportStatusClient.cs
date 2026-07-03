using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Api.Telemetry;

namespace SubjectHitman.Api.Infrastructure;

/// <summary>
/// Настройки клиента внешнего API статуса отчётов.
/// </summary>
public class ReportStatusApiOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "ReportStatusApi";

    /// <summary>Базовый URL API статусов.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("http://localhost:5100");

    /// <summary>Таймаут HTTP-запроса.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// HTTP-реализация <see cref="IReportStatusClient"/> через
/// <c>GET /reports/{reportId}/status</c>. Никогда не выбрасывает исключений: любая ошибка
/// транспорта или протокола возвращает <see cref="ReportStatus.Unknown"/>
/// (повторные попытки — зона ответственности саги).
/// </summary>
/// <param name="httpClient">Настроенный HTTP-клиент.</param>
/// <param name="metrics">Публикатор метрик прикладного уровня.</param>
/// <param name="logger">Логгер.</param>
public class ReportStatusClient(HttpClient httpClient, IApiMetricsPublisher metrics, ILogger<ReportStatusClient> logger) : IReportStatusClient
{
    /// <inheritdoc />
    public async Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["ReportId"] = reportId });
        logger.LogDebug("Requesting report status from status API");
        using var __ = metrics.MeasureReportStatusRequestDuration();
        try
        {
            var response = await httpClient.GetAsync(new Uri($"reports/{reportId}/status", UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Status API returned {StatusCode}, treating as Unknown",
                    (int)response.StatusCode);
                metrics.ReportStatusRequestCompleted("unknown");
                return ReportStatus.Unknown;
            }

            var body = await response.Content.ReadFromJsonAsync<ReportStatusResponse>(ct);
            var result = body?.Status ?? ReportStatus.Unknown;
            metrics.ReportStatusRequestCompleted(result.ToString().ToLowerInvariant());
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Status API call failed, treating as Unknown");
            metrics.ReportStatusRequestCompleted("unknown");
            return ReportStatus.Unknown;
        }
    }
}

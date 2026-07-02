using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;

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
/// <param name="logger">Логгер.</param>
public class ReportStatusClient(HttpClient httpClient, ILogger<ReportStatusClient> logger) : IReportStatusClient
{
    /// <inheritdoc />
    public async Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.GetAsync(new Uri($"reports/{reportId}/status", UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Status API returned {StatusCode} for report {ReportId}, treating as Unknown",
                    (int)response.StatusCode,
                    reportId);
                return ReportStatus.Unknown;
            }

            var body = await response.Content.ReadFromJsonAsync<ReportStatusResponse>(ct);
            return body?.Status ?? ReportStatus.Unknown;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Status API call failed for report {ReportId}, treating as Unknown", reportId);
            return ReportStatus.Unknown;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;

namespace SubjectHitman.Api.Infrastructure;

/// <summary>
/// Settings of the external report status API client.
/// </summary>
public class ReportStatusApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ReportStatusApi";

    /// <summary>Base URL of the status API.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("http://localhost:5100");

    /// <summary>HTTP request timeout.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// HTTP implementation of <see cref="IReportStatusClient"/> over
/// <c>GET /reports/{reportId}/status</c>. Never throws: any transport or protocol error
/// yields <see cref="ReportStatus.Unknown"/> (retries are the saga's responsibility).
/// </summary>
/// <param name="httpClient">Configured HTTP client.</param>
/// <param name="logger">Logger.</param>
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

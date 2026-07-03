using System.ComponentModel.DataAnnotations;

namespace SubjectHitman.Domain.Counting;

/// <summary>
/// Настройки логики подсчёта бесплатных отчётов.
/// </summary>
public class FreeReportsOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "FreeReports";

    /// <summary>
    /// Период кулдауна: бесплатные отчёты, предоставленные в пределах этого интервала от первого отчёта группы,
    /// считаются как один отчёт (защита от дублирующих заказов).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Идентификатор IANA часового пояса, в котором рассчитываются границы календарного года.
    /// </summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Moscow";
}

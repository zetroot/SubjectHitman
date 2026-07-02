using System.ComponentModel.DataAnnotations;

namespace SubjectHitman.Api.Sagas;

/// <summary>
/// Настройки саги учёта отчётов.
/// </summary>
public class SagaOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Saga";

    /// <summary>
    /// Интервал, по истечении которого ожидающая сага проверяет статус отчёта в основной системе.
    /// Также интервал между последовательными проверками.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Максимальное количество проверок статуса. При превышении, если статус всё ещё неизвестен,
    /// отчёт учитывается как неоплаченный, и сага завершается.
    /// </summary>
    [Range(1, 1000)]
    public int MaxTimeoutRetries { get; set; } = 5;
}

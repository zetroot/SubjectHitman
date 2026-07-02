namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Учётная запись одного заказанного кредитного отчёта.
/// Первичный ключ <see cref="ReportId"/> также служит ключом идемпотентности для событий учёта.
/// </summary>
public class ReportUsage
{
    /// <summary>Идентификатор заказа отчёта (из события <c>ReportOrdered</c>).</summary>
    public Guid ReportId { get; set; }

    /// <summary>Идентификатор субъекта, для которого был заказан отчёт.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Признак того, что отчёт бесплатный для субъекта.</summary>
    public bool IsFree { get; set; }

    /// <summary>Текущий статус учёта отчёта.</summary>
    public ReportUsageStatus Status { get; set; }

    /// <summary>Момент заказа отчёта. Основа для отнесения к календарному году и cooldown-группировки.</summary>
    public DateTimeOffset OrderedAt { get; set; }

    /// <summary>Момент завершения учёта (списан или не списан); <see langword="null"/> пока в ожидании.</summary>
    public DateTimeOffset? FinishedAt { get; set; }
}

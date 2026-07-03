namespace SubjectHitman.Domain.Entities;

/// <summary>
/// Статус учёта заказанного отчёта.
/// </summary>
public enum ReportUsageStatus : short
{
    /// <summary>Отчёт заказан; общая сага обработки ещё не завершилась.</summary>
    Pending = 0,

    /// <summary>Отчёт изготовлен и предоставлен; учитывается в счёт бесплатной квоты субъекта.</summary>
    Charged = 1,

    /// <summary>Отчёт не был изготовлен; не учитывается в счёт квоты.</summary>
    NotCharged = 2,
}

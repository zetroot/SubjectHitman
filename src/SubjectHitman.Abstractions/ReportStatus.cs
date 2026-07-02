namespace SubjectHitman.Abstractions;

/// <summary>
/// Статус отчёта в основной системе обработки отчётов,
/// возвращаемый внешним API статуса (запрашивается в timeout-ветке саги).
/// </summary>
public enum ReportStatus
{
    /// <summary>Статус не удалось определить (используется также при ошибках транспорта и таймаутах).</summary>
    Unknown = 0,

    /// <summary>Отчёт успешно изготовлен и предоставлен клиенту.</summary>
    Success = 1,

    /// <summary>Изготовление отчёта завершилось неуспешно.</summary>
    Failed = 2,
}

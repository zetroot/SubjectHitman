using SubjectHitman.Domain.Entities;

namespace SubjectHitman.Domain.Repositories;

/// <summary>
/// Репозиторий учётных записей отчётов. Предоставляет операции поиска, добавления
/// и агрегации записей <see cref="ReportUsage"/>.
/// </summary>
public interface IReportUsageRepository
{
    /// <summary>
    /// Находит учётную запись отчёта по идентификатору отчёта.
    /// </summary>
    /// <param name="reportId">Идентификатор отчёта (первичный ключ).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Запись или <see langword="null"/>, если не найдена.</returns>
    Task<ReportUsage?> FindAsync(Guid reportId, CancellationToken ct);

    /// <summary>
    /// Добавляет учётную запись отчёта в контекст. Сохранение — вызовом <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="usage">Новая учётная запись.</param>
    void Add(ReportUsage usage);

    /// <summary>
    /// Возвращает отметки времени заказа списанных бесплатных отчётов субъекта за указанный период,
    /// отсортированные по возрастанию. Используется для подсчёта с группировкой по кулдауну.
    /// </summary>
    /// <param name="subjectId">Идентификатор субъекта.</param>
    /// <param name="fromUtc">Нижняя граница периода (UTC, включительно).</param>
    /// <param name="toUtc">Верхняя граница периода (UTC, исключительно).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Отсортированный по возрастанию список отметок <see cref="ReportUsage.OrderedAt"/>.</returns>
    Task<List<DateTimeOffset>> GetChargedFreeOrderTimestampsAsync(
        Guid subjectId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct);

    /// <summary>
    /// Сохраняет все ожидающие изменения в базе данных.
    /// Явный вызов обязателен — Wolverine <c>AutoApplyTransactions</c> не флашит пользовательские сущности.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Количество затронутых строк.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct);
}

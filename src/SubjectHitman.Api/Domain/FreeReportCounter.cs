using Microsoft.Extensions.Options;
using SubjectHitman.Domain.Entities;
using SubjectHitman.Domain.Repositories;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Результат подсчёта использованных бесплатных отчётов.
/// </summary>
/// <param name="UsedFreeReportsCount">Количество оплаченных бесплатных отчётов в периоде с учётом группировки по кулдауну.</param>
/// <param name="PeriodStart">Включающая нижняя граница периода календарного года.</param>
/// <param name="PeriodEnd">Включающая верхняя граница периода календарного года (последняя секунда года).</param>
public record FreeReportCountResult(int UsedFreeReportsCount, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);

/// <summary>
/// Подсчитывает бесплатные отчёты, предоставленные субъекту в текущем календарном году,
/// группируя отчёты внутри периода кулдауна в один (техническая спецификация, § 6).
/// </summary>
/// <param name="reportUsageRepository">Репозиторий учётных записей отчётов.</param>
/// <param name="options">Настройки подсчёта (кулдаун, часовой пояс).</param>
/// <param name="timeProvider">Источник времени, определяющий «сейчас».</param>
public class FreeReportCounter(
    IReportUsageRepository reportUsageRepository,
    IOptions<FreeReportsOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Подсчитывает оплаченные бесплатные отчёты субъекта за текущий календарный год с учётом группировки по кулдауну.
    /// </summary>
    /// <param name="subjectId">Внутренний идентификатор субъекта.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Количество и границы периода календарного года.</returns>
    public async Task<FreeReportCountResult> CountAsync(Guid subjectId, CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var nowLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz);
        var startLocal = new DateTimeOffset(nowLocal.Year, 1, 1, 0, 0, 0, nowLocal.Offset);
        // Recompute the offset at the period boundary itself (DST-safe for arbitrary zones).
        var startUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), tz),
            TimeSpan.Zero);
        var endUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), tz),
            TimeSpan.Zero);

        var orderedAts = await reportUsageRepository.GetChargedFreeOrderTimestampsAsync(subjectId, startUtc, endUtc, ct);

        var count = CollapseByCooldown(orderedAts, options.Value.CooldownPeriod);

        return new FreeReportCountResult(
            count,
            TimeZoneInfo.ConvertTime(startUtc, tz),
            TimeZoneInfo.ConvertTime(endUtc - TimeSpan.FromSeconds(1), tz));
    }

    /// <summary>
    /// Группирует хронологически упорядоченную последовательность отметок времени отчётов:
    /// отчёт принадлежит текущей группе, если он находится в пределах <paramref name="cooldown"/>
    /// от ПЕРВОГО отчёта группы (строго большая разница открывает новую группу).
    /// </summary>
    /// <param name="orderedTimestamps">Отметки времени, отсортированные по возрастанию.</param>
    /// <param name="cooldown">Период кулдауна.</param>
    /// <returns>Количество групп.</returns>
    public static int CollapseByCooldown(IReadOnlyList<DateTimeOffset> orderedTimestamps, TimeSpan cooldown)
    {
        ArgumentNullException.ThrowIfNull(orderedTimestamps);

        var count = 0;
        DateTimeOffset? groupStart = null;
        foreach (var ts in orderedTimestamps)
        {
            if (groupStart is null || ts - groupStart.Value > cooldown)
            {
                count++;
                groupStart = ts;
            }
        }

        return count;
    }
}

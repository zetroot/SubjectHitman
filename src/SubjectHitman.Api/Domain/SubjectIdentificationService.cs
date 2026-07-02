using Microsoft.EntityFrameworkCore;
using Npgsql;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Domain.Entities;
using SubjectHitman.Api.Infrastructure;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Идентифицирует субъекта кредитной истории в локальном реестре по поисковым ключам,
/// построенным согласно Указанию 5791-У: находит существующего субъекта (создавая нового при отсутствии),
/// объединяет входящие персональные данные и пересчитывает поисковые ключи (техническая спецификация, § 5.4–5.6).
/// </summary>
/// <param name="dbContext">Контекст базы данных.</param>
/// <param name="timeProvider">Источник времени, используемый для отметки создания субъекта.</param>
/// <param name="logger">Логгер.</param>
public class SubjectIdentificationService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SubjectIdentificationService> logger)
{
    private const int MaxRetries = 1;

    /// <summary>
    /// Идентифицирует субъекта, описанного в <paramref name="data"/>: вычисляет поисковые ключи запроса,
    /// ищет кандидатов, выбирает победителя по правилам разрешения конфликтов, объединяет персональные данные
    /// и пересчитывает сохранённые ключи. Создаёт нового субъекта, если ничего не найдено.
    /// Вся операция выполняется в сериализуемой единице, защищённой консультативными блокировками PostgreSQL
    /// на хешах ключей запроса, и повторяется один раз при гонке по уникальному ограничению.
    /// </summary>
    /// <param name="data">Необработанные персональные данные субъекта из запроса или события.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Внутренний идентификатор идентифицированного или созданного субъекта.</returns>
    public async Task<Guid> IdentifyAsync(SubjectData data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);

        var normalized = NormalizedSubject.FromSubjectData(data);
        var requestKeys = SearchKeyBuilder.Build(normalized);
        if (requestKeys.Count == 0)
        {
            throw new InvalidOperationException("No search keys could be computed from the request data.");
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await IdentifyCoreAsync(normalized, requestKeys, ct);
            }
            catch (DbUpdateException ex) when (
                attempt < MaxRetries && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                logger.LogWarning("Unique violation during subject identification, retrying (attempt {Attempt})", attempt + 1);
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Guid> IdentifyCoreAsync(
        NormalizedSubject normalized,
        IReadOnlyCollection<SearchKeyValue> requestKeys,
        CancellationToken ct)
    {
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            await AcquireAdvisoryLocksAsync(requestKeys, ct);

            var hashes = requestKeys.Select(k => k.Hash).ToList();
            var matches = await dbContext.SearchKeys
                .Where(k => hashes.Contains(k.Hash))
                .Select(k => new { k.SubjectId, k.KeyType, k.Hash })
                .ToListAsync(ct);

            // Advisory hash prefixes may collide across key types in theory; filter to exact request keys.
            var requestKeySet = requestKeys.ToHashSet();
            var confirmed = matches
                .Where(m => requestKeySet.Contains(new SearchKeyValue(m.KeyType, m.Hash)))
                .GroupBy(m => m.SubjectId)
                .ToList();

            Guid subjectId;
            if (confirmed.Count == 0)
            {
                subjectId = await CreateSubjectAsync(normalized, ct);
                logger.LogInformation("Created new subject {SubjectId} with {KeyCount} search keys", subjectId, requestKeys.Count);
            }
            else
            {
                subjectId = await PickWinnerAsync(confirmed.ToDictionary(
                    g => g.Key,
                    g => g.Select(m => m.KeyType).Distinct().OrderBy(t => t).ToList()), ct);
                await MergeSubjectAsync(subjectId, normalized, ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return subjectId;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task AcquireAdvisoryLocksAsync(IReadOnlyCollection<SearchKeyValue> keys, CancellationToken ct)
    {
        var lockIds = keys
            .Select(k => BitConverter.ToInt64(k.Hash, 0))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        foreach (var lockId in lockIds)
        {
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
        }
    }

    private async Task<Guid> PickWinnerAsync(Dictionary<Guid, List<SearchKeyType>> candidates, CancellationToken ct)
    {
        if (candidates.Count == 1)
        {
            return candidates.Keys.Single();
        }

        var createdAt = await dbContext.Subjects
            .Where(s => candidates.Keys.Contains(s.Id))
            .Select(s => new { s.Id, s.CreatedAt })
            .ToDictionaryAsync(s => s.Id, s => s.CreatedAt, ct);

        var winner = ResolveWinner(candidates, createdAt);

        logger.LogInformation(
            "Ambiguous identification resolved: {CandidateCount} candidates, winner {SubjectId} with {MatchCount} matched keys [{KeyTypes}]",
            candidates.Count,
            winner,
            candidates[winner].Count,
            string.Join(",", candidates[winner]));

        return winner;
    }

    /// <summary>
    /// Определяет победителя среди нескольких найденных субъектов (техническая спецификация, § 5.4):
    /// 1) наибольшее количество совпавших ключей; 2) самые сильные совпавшие ключи — лексикографическое
    /// сравнение списков типов ключей, отсортированных по возрастанию; 3) самая ранняя запись.
    /// </summary>
    /// <param name="candidates">Типы совпавших ключей для каждого субъекта-кандидата (каждый список отсортирован по возрастанию).</param>
    /// <param name="createdAt">Отметки времени создания субъектов-кандидатов.</param>
    /// <returns>Идентификатор победившего субъекта.</returns>
    public static Guid ResolveWinner(
        IReadOnlyDictionary<Guid, List<SearchKeyType>> candidates,
        IReadOnlyDictionary<Guid, DateTimeOffset> createdAt)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(createdAt);

        return candidates
            .OrderByDescending(c => c.Value.Count)
            .ThenBy(c => string.Join(",", c.Value.Select(t => (int)t)), StringComparer.Ordinal)
            .ThenBy(c => createdAt[c.Key])
            .First()
            .Key;
    }

    private async Task<Guid> CreateSubjectAsync(NormalizedSubject normalized, CancellationToken ct)
    {
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            BirthDate = normalized.BirthDate,
            Inn = normalized.Inn,
            Snils = normalized.Snils,
            CreatedAt = timeProvider.GetUtcNow(),
            Names = normalized.Names
                .Select(n => new SubjectName { LastName = n.LastName, FirstName = n.FirstName, MiddleName = n.MiddleName })
                .ToList(),
            Documents = normalized.Documents
                .Select(d => new SubjectDocument { TypeCode = d.TypeCode, Series = d.Series, Number = d.Number, IssueDate = d.IssueDate })
                .ToList(),
        };
        subject.SearchKeys = SearchKeyBuilder.Build(normalized)
            .Select(k => new SearchKey { KeyType = k.KeyType, Hash = k.Hash })
            .ToList();

        dbContext.Subjects.Add(subject);
        await dbContext.SaveChangesAsync(ct);
        return subject.Id;
    }

    private async Task MergeSubjectAsync(Guid subjectId, NormalizedSubject incoming, CancellationToken ct)
    {
        var subject = await dbContext.Subjects
            .Include(s => s.Names)
            .Include(s => s.Documents)
            .Include(s => s.SearchKeys)
            .SingleAsync(s => s.Id == subjectId, ct);

        var changed = false;

        foreach (var name in incoming.Names)
        {
            if (!subject.Names.Any(n =>
                    n.LastName == name.LastName && n.FirstName == name.FirstName && n.MiddleName == name.MiddleName))
            {
                subject.Names.Add(new SubjectName
                {
                    LastName = name.LastName,
                    FirstName = name.FirstName,
                    MiddleName = name.MiddleName,
                });
                changed = true;
            }
        }

        foreach (var doc in incoming.Documents)
        {
            if (!subject.Documents.Any(d =>
                    d.TypeCode == doc.TypeCode && d.Series == doc.Series && d.Number == doc.Number && d.IssueDate == doc.IssueDate))
            {
                subject.Documents.Add(new SubjectDocument
                {
                    TypeCode = doc.TypeCode,
                    Series = doc.Series,
                    Number = doc.Number,
                    IssueDate = doc.IssueDate,
                });
                changed = true;
            }
        }

        changed |= MergeScalar(subject, s => s.BirthDate, (s, v) => s.BirthDate = v, incoming.BirthDate, "BirthDate");
        changed |= MergeScalar(subject, s => s.Inn, (s, v) => s.Inn = v, incoming.Inn, "Inn");
        changed |= MergeScalar(subject, s => s.Snils, (s, v) => s.Snils = v, incoming.Snils, "Snils");

        if (changed)
        {
            var merged = NormalizedSubject.FromStored(
                subject.Names.Select(n => new NormalizedName(n.LastName, n.FirstName, n.MiddleName)),
                subject.Documents.Select(d => new NormalizedDocument(d.TypeCode, d.Series, d.Number, d.IssueDate)),
                subject.BirthDate,
                subject.Inn,
                subject.Snils);

            subject.SearchKeys.Clear();
            foreach (var key in SearchKeyBuilder.Build(merged))
            {
                subject.SearchKeys.Add(new SearchKey { KeyType = key.KeyType, Hash = key.Hash });
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private bool MergeScalar<T>(
        Subject subject,
        Func<Subject, T?> getter,
        Action<Subject, T?> setter,
        T? incoming,
        string fieldName)
    {
        var current = getter(subject);
        if (incoming is null)
        {
            return false;
        }

        if (current is null)
        {
            setter(subject, incoming);
            return true;
        }

        if (!EqualityComparer<T?>.Default.Equals(current, incoming))
        {
            // Q1 decision: keep the stored value, log a warning without exposing personal data.
            logger.LogWarning(
                "Conflicting {Field} for subject {SubjectId}: incoming value differs from stored, keeping stored",
                fieldName,
                subject.Id);
        }

        return false;
    }
}

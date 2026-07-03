using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SubjectHitman.Domain;
using SubjectHitman.Domain.Entities;
using SubjectHitman.Domain.Repositories;

namespace SubjectHitman.DataAccess.Repositories;

/// <summary>
/// Реализация <see cref="ISubjectRepository"/> поверх <see cref="AppDbContext"/>.
/// Операция идентификации выполняется в сериализуемой области с advisory-блокировками PostgreSQL
/// и автоматическим ретраем при гонках уникального ограничения (ровно 1 повтор).
/// </summary>
/// <param name="dbContext">Контекст базы данных.</param>
/// <param name="logger">Логгер.</param>
public sealed class SubjectRepository(
    AppDbContext dbContext,
    ILogger<SubjectRepository> logger) : ISubjectRepository
{
    private const int MaxRetries = 1;

    /// <inheritdoc />
    public async Task<T> ExecuteIdentificationAsync<T>(
        IReadOnlyCollection<SearchKeyValue> requestKeys,
        Func<ISubjectRepository, CancellationToken, Task<T>> body,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ExecuteInTransactionAsync(requestKeys, body, ct);
            }
            catch (DbUpdateException ex) when (
                attempt < MaxRetries && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                logger.LogWarning("Unique violation during subject identification, retrying (attempt {Attempt})", attempt + 1);
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    /// <inheritdoc />
    public async Task<List<KeyMatch>> FindKeyMatchesAsync(IReadOnlyCollection<byte[]> hashes, CancellationToken ct)
    {
        return await dbContext.SearchKeys
            .Where(k => hashes.Contains(k.Hash))
            .Select(k => new KeyMatch(k.SubjectId, k.KeyType, k.Hash))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, DateTimeOffset>> GetCreatedAtAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken ct)
    {
        return await dbContext.Subjects
            .Where(s => subjectIds.Contains(s.Id))
            .Select(s => new { s.Id, s.CreatedAt })
            .ToDictionaryAsync(s => s.Id, s => s.CreatedAt, ct);
    }

    /// <inheritdoc />
    public Task AddSubjectAsync(Subject subject, CancellationToken ct)
    {
        dbContext.Subjects.Add(subject);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Subject> GetSubjectWithDetailsAsync(Guid subjectId, CancellationToken ct)
    {
        return await dbContext.Subjects
            .Include(s => s.Names)
            .Include(s => s.Documents)
            .Include(s => s.SearchKeys)
            .SingleAsync(s => s.Id == subjectId, ct);
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct)
    {
        return dbContext.SaveChangesAsync(ct);
    }

    private async Task<T> ExecuteInTransactionAsync<T>(
        IReadOnlyCollection<SearchKeyValue> requestKeys,
        Func<ISubjectRepository, CancellationToken, Task<T>> body,
        CancellationToken ct)
    {
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            await AcquireAdvisoryLocksAsync(requestKeys, ct);
            var result = await body(this, ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return result;
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
}

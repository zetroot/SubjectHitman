using SubjectHitman.Domain.Entities;

namespace SubjectHitman.Domain.Repositories;

/// <summary>
/// Подтверждённое совпадение поискового ключа — результат поиска по хешам в репозитории.
/// </summary>
/// <param name="SubjectId">Идентификатор субъекта, которому принадлежит ключ.</param>
/// <param name="KeyType">Тип совпавшего ключа.</param>
/// <param name="Hash">Хеш совпавшего ключа.</param>
public readonly record struct KeyMatch(Guid SubjectId, SearchKeyType KeyType, byte[] Hash);

/// <summary>
/// Репозиторий субъектов кредитных историй. Предоставляет операции поиска, создания и обогащения
/// субъектов. Операция идентификации выполняется в сериализуемой области с advisory-блокировками
/// PostgreSQL и автоматическим ретраем при гонках уникального ограничения.
/// </summary>
public interface ISubjectRepository
{
    /// <summary>
    /// Выполняет операцию идентификации в сериализуемой области:
    /// открывает транзакцию (если нет текущей), берёт advisory-блокировки на хешах ключей запроса,
    /// вызывает переданную бизнес-логику (<paramref name="body"/>), коммитит.
    /// При возникновении <c>unique violation</c> в ходе выполнения <paramref name="body"/>
    /// сбрасывает трекинг и повторяет попытку ровно один раз.
    /// </summary>
    /// <typeparam name="T">Тип результата операции идентификации.</typeparam>
    /// <param name="requestKeys">Поисковые ключи запроса, по которым берутся advisory-блокировки.</param>
    /// <param name="body">Бизнес-логика идентификации, получающая репозиторий и токен отмены.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения <paramref name="body"/>.</returns>
    Task<T> ExecuteIdentificationAsync<T>(
        IReadOnlyCollection<SearchKeyValue> requestKeys,
        Func<ISubjectRepository, CancellationToken, Task<T>> body,
        CancellationToken ct);

    /// <summary>
    /// Возвращает подтверждённые совпадения поисковых ключей: для каждого из переданных хешей
    /// находит записи <see cref="SearchKey"/> и проецирует их в <see cref="KeyMatch"/>.
    /// </summary>
    /// <param name="hashes">Хеши поисковых ключей для поиска.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список совпадений (может быть пустым).</returns>
    Task<List<KeyMatch>> FindKeyMatchesAsync(IReadOnlyCollection<byte[]> hashes, CancellationToken ct);

    /// <summary>
    /// Возвращает отметки времени создания (<see cref="Subject.CreatedAt"/>) для указанных субъектов.
    /// </summary>
    /// <param name="subjectIds">Идентификаторы субъектов.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Словарь <c>SubjectId → CreatedAt</c>; отсутствующие субъекты в словаре отсутствуют.</returns>
    Task<Dictionary<Guid, DateTimeOffset>> GetCreatedAtAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken ct);

    /// <summary>
    /// Добавляет нового субъекта в контекст. Сохранение — вызовом <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="subject">Новый субъект с заполненными именами, документами и ключами.</param>
    /// <param name="ct">Токен отмены.</param>
    Task AddSubjectAsync(Subject subject, CancellationToken ct);

    /// <summary>
    /// Загружает субъекта с полным графом связанных сущностей (имена, документы, ключи поиска),
    /// необходимым для операции слияния (<c>MergeSubjectAsync</c>).
    /// </summary>
    /// <param name="subjectId">Идентификатор субъекта.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Субъект с загруженными навигационными свойствами.</returns>
    Task<Subject> GetSubjectWithDetailsAsync(Guid subjectId, CancellationToken ct);

    /// <summary>
    /// Сохраняет все ожидающие изменения в базе данных.
    /// Явный вызов обязателен — Wolverine <c>AutoApplyTransactions</c> не флашит пользовательские сущности.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Количество затронутых строк.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct);
}

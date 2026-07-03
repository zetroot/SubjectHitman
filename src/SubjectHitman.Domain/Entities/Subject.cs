namespace SubjectHitman.Domain.Entities;

/// <summary>
/// Мастер-запись субъекта кредитной истории (гражданина РФ).
/// Владеет коллекциями ФИО, документов, удостоверяющих личность, и ключей поиска.
/// </summary>
public class Subject
{
    /// <summary>Внутренний уникальный идентификатор субъекта.</summary>
    public Guid Id { get; set; }

    /// <summary>Дата рождения, если известна. Мощность 0..1.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>ИНН (только цифры), если известен. Мощность 0..1.</summary>
    public string? Inn { get; set; }

    /// <summary>СНИЛС (только цифры), если известен. Мощность 0..1.</summary>
    public string? Snils { get; set; }

    /// <summary>
    /// Момент создания (UTC). Используется как финальный критерий при равной степени
    /// совпадения кандидатов: выигрывает более старая запись.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Все известные ФИО субъекта (текущие и предыдущие, без различения).</summary>
    public List<SubjectName> Names { get; set; } = [];

    /// <summary>Все известные документы, удостоверяющие личность субъекта (текущие и предыдущие, без различения).</summary>
    public List<SubjectDocument> Documents { get; set; } = [];

    /// <summary>Предвычисленные ключи поиска субъекта.</summary>
    public List<SearchKey> SearchKeys { get; set; } = [];
}

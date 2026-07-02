namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Документ, удостоверяющий личность субъекта. Субъект может иметь несколько документов;
/// различие между текущим и предыдущим документом не хранится.
/// Все значения хранятся в нормализованном виде (см. техническую спецификацию, § 5.1).
/// </summary>
public class SubjectDocument
{
    /// <summary>Суррогатный идентификатор.</summary>
    public long Id { get; set; }

    /// <summary>Идентификатор субъекта-владельца.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Код вида документа по классификатору Банка России.</summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>Серия документа; пустая строка, если у документа нет серии.</summary>
    public string Series { get; set; } = string.Empty;

    /// <summary>Номер документа.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// Дата выдачи. Может быть <see langword="null"/> для предыдущего документа,
    /// полученного без даты; такие документы не участвуют в ключе поиска K3.
    /// </summary>
    public DateOnly? IssueDate { get; set; }
}

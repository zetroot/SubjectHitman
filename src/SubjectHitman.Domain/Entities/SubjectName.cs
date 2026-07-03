namespace SubjectHitman.Domain.Entities;

/// <summary>
/// ФИО субъекта. Субъект может иметь несколько вариантов ФИО (например, до и после смены фамилии);
/// различие между текущим и предыдущим ФИО не хранится.
/// Все значения хранятся в нормализованном виде (см. техническую спецификацию, § 5.1).
/// </summary>
public class SubjectName
{
    /// <summary>Суррогатный идентификатор.</summary>
    public long Id { get; set; }

    /// <summary>Идентификатор субъекта-владельца.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Нормализованная фамилия; <c>"-"</c> при отсутствии.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Нормализованное имя; <c>"-"</c> при отсутствии.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Нормализованное отчество; <c>"-"</c> при отсутствии.</summary>
    public string MiddleName { get; set; } = string.Empty;
}

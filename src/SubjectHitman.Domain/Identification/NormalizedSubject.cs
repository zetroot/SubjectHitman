using SubjectHitman.Abstractions;

namespace SubjectHitman.Domain.Identification;

/// <summary>
/// Нормализованное полное имя (все компоненты в верхнем регистре, транслитерированы, <c>"-"</c> для отсутствующих частей).
/// </summary>
/// <param name="LastName">Нормализованная фамилия.</param>
/// <param name="FirstName">Нормализованное имя.</param>
/// <param name="MiddleName">Нормализованное отчество.</param>
public readonly record struct NormalizedName(string LastName, string FirstName, string MiddleName);

/// <summary>
/// Нормализованный документ, удостоверяющий личность (код типа, серия, номер в верхнем регистре; пустая серия при отсутствии).
/// </summary>
/// <param name="TypeCode">Нормализованный код типа документа.</param>
/// <param name="Series">Нормализованная серия; пустая строка при отсутствии.</param>
/// <param name="Number">Нормализованный номер документа.</param>
/// <param name="IssueDate">Дата выдачи; <see langword="null"/>, если не указана (только для предыдущего документа).</param>
public readonly record struct NormalizedDocument(string TypeCode, string Series, string Number, DateOnly? IssueDate);

/// <summary>
/// Персональные данные субъекта после нормализации (техническая спецификация, § 5.1), единый источник как для вычисления поисковых ключей, так и для хранения.
/// </summary>
public sealed class NormalizedSubject
{
    private NormalizedSubject(
        IReadOnlyList<NormalizedName> names,
        IReadOnlyList<NormalizedDocument> documents,
        DateOnly? birthDate,
        string? inn,
        string? snils)
    {
        Names = names;
        Documents = documents;
        BirthDate = birthDate;
        Inn = inn;
        Snils = snils;
    }

    /// <summary>Уникальные нормализованные полные имена (текущие + предыдущие).</summary>
    public IReadOnlyList<NormalizedName> Names { get; }

    /// <summary>Уникальные нормализованные документы, удостоверяющие личность (текущие + предыдущие).</summary>
    public IReadOnlyList<NormalizedDocument> Documents { get; }

    /// <summary>Дата рождения, если указана.</summary>
    public DateOnly? BirthDate { get; }

    /// <summary>ИНН, содержащий только цифры, или <see langword="null"/> при отсутствии.</summary>
    public string? Inn { get; }

    /// <summary>СНИЛС, содержащий только цифры, или <see langword="null"/> при отсутствии.</summary>
    public string? Snils { get; }

    /// <summary>
    /// Нормализует необработанные данные субъекта, полученные из внешней системы.
    /// </summary>
    /// <param name="data">Необработанные персональные данные субъекта.</param>
    /// <returns>Нормализованное представление с дедуплицированными именами и документами.</returns>
    public static NormalizedSubject FromSubjectData(SubjectData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var names = new List<NormalizedName>
        {
            NormalizeName(data.LastName, data.FirstName, data.MiddleName),
        };
        if (data.PreviousName is { } prev)
        {
            names.Add(NormalizeName(prev.LastName, prev.FirstName, prev.MiddleName));
        }

        var documents = new List<NormalizedDocument> { NormalizeDocument(data.Document) };
        if (data.PreviousDocument is { } prevDoc)
        {
            documents.Add(NormalizeDocument(prevDoc));
        }

        return new NormalizedSubject(
            names.Distinct().ToList(),
            documents.Distinct().ToList(),
            data.BirthDate,
            PersonalDataNormalizer.NormalizeDigitsOnly(data.Inn),
            PersonalDataNormalizer.NormalizeDigitsOnly(data.Snils));
    }

    /// <summary>
    /// Создаёт нормализованное представление из уже нормализованных сохранённых значений субъекта.
    /// </summary>
    /// <param name="names">Сохранённые имена (уже нормализованные).</param>
    /// <param name="documents">Сохранённые документы (уже нормализованные).</param>
    /// <param name="birthDate">Сохранённая дата рождения.</param>
    /// <param name="inn">Сохранённый ИНН, содержащий только цифры.</param>
    /// <param name="snils">Сохранённый СНИЛС, содержащий только цифры.</param>
    /// <returns>Нормализованное представление.</returns>
    public static NormalizedSubject FromStored(
        IEnumerable<NormalizedName> names,
        IEnumerable<NormalizedDocument> documents,
        DateOnly? birthDate,
        string? inn,
        string? snils)
        => new(names.Distinct().ToList(), documents.Distinct().ToList(), birthDate, inn, snils);

    private static NormalizedName NormalizeName(string? last, string? first, string? middle) => new(
        PersonalDataNormalizer.NormalizeNameComponent(last),
        PersonalDataNormalizer.NormalizeNameComponent(first),
        PersonalDataNormalizer.NormalizeNameComponent(middle));

    private static NormalizedDocument NormalizeDocument(IdentityDocumentData doc) => new(
        PersonalDataNormalizer.NormalizeDocumentField(doc.TypeCode),
        PersonalDataNormalizer.NormalizeDocumentField(doc.Series),
        PersonalDataNormalizer.NormalizeDocumentField(doc.Number),
        doc.IssueDate);
}

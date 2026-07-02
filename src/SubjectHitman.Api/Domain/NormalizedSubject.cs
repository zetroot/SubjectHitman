using SubjectHitman.Abstractions;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// A normalized full name (all components upper-cased, transliterated, <c>"-"</c> for absent parts).
/// </summary>
/// <param name="LastName">Normalized last name.</param>
/// <param name="FirstName">Normalized first name.</param>
/// <param name="MiddleName">Normalized middle name.</param>
public readonly record struct NormalizedName(string LastName, string FirstName, string MiddleName);

/// <summary>
/// A normalized identity document (type code, series, number upper-cased; empty series for absent).
/// </summary>
/// <param name="TypeCode">Normalized document type code.</param>
/// <param name="Series">Normalized series; empty string when absent.</param>
/// <param name="Number">Normalized document number.</param>
/// <param name="IssueDate">Issue date; <see langword="null"/> when not provided (previous document only).</param>
public readonly record struct NormalizedDocument(string TypeCode, string Series, string Number, DateOnly? IssueDate);

/// <summary>
/// Subject personal data after normalization (technical spec, § 5.1), the single source
/// for both search-key computation and persistence.
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

    /// <summary>Distinct normalized full names (current + previous).</summary>
    public IReadOnlyList<NormalizedName> Names { get; }

    /// <summary>Distinct normalized identity documents (current + previous).</summary>
    public IReadOnlyList<NormalizedDocument> Documents { get; }

    /// <summary>Date of birth, when present.</summary>
    public DateOnly? BirthDate { get; }

    /// <summary>Digits-only INN, or <see langword="null"/> when absent.</summary>
    public string? Inn { get; }

    /// <summary>Digits-only SNILS, or <see langword="null"/> when absent.</summary>
    public string? Snils { get; }

    /// <summary>
    /// Normalizes raw subject data received from an external system.
    /// </summary>
    /// <param name="data">Raw subject personal data.</param>
    /// <returns>The normalized representation with deduplicated names and documents.</returns>
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
    /// Builds a normalized view from already-normalized stored values of a subject.
    /// </summary>
    /// <param name="names">Stored names (already normalized).</param>
    /// <param name="documents">Stored documents (already normalized).</param>
    /// <param name="birthDate">Stored date of birth.</param>
    /// <param name="inn">Stored digits-only INN.</param>
    /// <param name="snils">Stored digits-only SNILS.</param>
    /// <returns>The normalized representation.</returns>
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

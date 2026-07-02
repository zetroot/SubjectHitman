namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Master record of a credit history subject (a citizen of the Russian Federation).
/// Owns collections of names, identity documents and search keys.
/// </summary>
public class Subject
{
    /// <summary>Internal unique identifier of the subject.</summary>
    public Guid Id { get; set; }

    /// <summary>Date of birth, when known. Cardinality 0..1.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Taxpayer number (ИНН), digits only, when known. Cardinality 0..1.</summary>
    public string? Inn { get; set; }

    /// <summary>Insurance account number (СНИЛС), digits only, when known. Cardinality 0..1.</summary>
    public string? Snils { get; set; }

    /// <summary>
    /// Creation timestamp (UTC). Used as the final tie-breaker when several candidate subjects
    /// match a request equally well: the oldest record wins.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>All known full names of the subject (current and previous, undistinguished).</summary>
    public List<SubjectName> Names { get; set; } = [];

    /// <summary>All known identity documents of the subject (current and previous, undistinguished).</summary>
    public List<SubjectDocument> Documents { get; set; } = [];

    /// <summary>Precomputed search keys of the subject.</summary>
    public List<SearchKey> SearchKeys { get; set; } = [];
}

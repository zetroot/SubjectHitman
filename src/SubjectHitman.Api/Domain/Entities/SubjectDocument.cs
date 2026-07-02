namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// An identity document of a subject. A subject may have multiple documents;
/// no distinction between current and previous documents is stored.
/// All values are stored in normalized form (see the technical spec, § 5.1).
/// </summary>
public class SubjectDocument
{
    /// <summary>Surrogate identifier.</summary>
    public long Id { get; set; }

    /// <summary>Owning subject identifier.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Document type code per the Bank of Russia classifier.</summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>Document series; empty string when the document has no series.</summary>
    public string Series { get; set; } = string.Empty;

    /// <summary>Document number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// Issue date. May be <see langword="null"/> for a previous document received without a date;
    /// such documents do not participate in the K3 search key.
    /// </summary>
    public DateOnly? IssueDate { get; set; }
}

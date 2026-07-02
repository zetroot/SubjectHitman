namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// A full name of a subject. A subject may have multiple names (e.g. before and after marriage);
/// no distinction between current and previous names is stored.
/// All values are stored in normalized form (see the technical spec, § 5.1).
/// </summary>
public class SubjectName
{
    /// <summary>Surrogate identifier.</summary>
    public long Id { get; set; }

    /// <summary>Owning subject identifier.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Normalized last name; <c>"-"</c> when absent.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Normalized first name; <c>"-"</c> when absent.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Normalized middle name; <c>"-"</c> when absent.</summary>
    public string MiddleName { get; set; } = string.Empty;
}

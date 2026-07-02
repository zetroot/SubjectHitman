namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// A precomputed search key of a subject: a SHA-256 hash over the normalized concatenation
/// of a subset of the subject's personal data (see the technical spec, § 5.3).
/// </summary>
public class SearchKey
{
    /// <summary>Surrogate identifier.</summary>
    public long Id { get; set; }

    /// <summary>Owning subject identifier.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Type of the key (K1..K6).</summary>
    public SearchKeyType KeyType { get; set; }

    /// <summary>32-byte SHA-256 hash of the canonical key preimage.</summary>
    public byte[] Hash { get; set; } = [];
}

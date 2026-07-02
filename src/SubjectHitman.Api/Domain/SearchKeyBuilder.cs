using System.Security.Cryptography;
using System.Text;
using SubjectHitman.Api.Domain.Entities;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// A computed search key value: its type and the SHA-256 hash of its canonical preimage.
/// </summary>
/// <param name="KeyType">Search key type (K1..K6).</param>
/// <param name="Hash">32-byte SHA-256 hash.</param>
public readonly record struct SearchKeyValue(SearchKeyType KeyType, byte[] Hash)
{
    /// <summary>Compares by key type and hash content (not reference).</summary>
    /// <param name="other">The other value.</param>
    /// <returns><see langword="true"/> when the type and hash bytes are equal.</returns>
    public bool Equals(SearchKeyValue other)
        => KeyType == other.KeyType && Hash.AsSpan().SequenceEqual(other.Hash);

    /// <summary>Hash code derived from the key type and the hash prefix.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
        => HashCode.Combine(KeyType, Hash.Length >= 4 ? BitConverter.ToInt32(Hash, 0) : 0);
}

/// <summary>
/// Computes the K1..K6 search keys of a subject from its normalized personal data
/// (technical spec, § 5.3). The same code path serves incoming requests and stored subjects,
/// guaranteeing hash comparability.
/// </summary>
public static class SearchKeyBuilder
{
    private const char Separator = '|';

    /// <summary>
    /// Builds all computable search keys for the given normalized subject data.
    /// Keys whose required indicators are absent are skipped. Multiplicity rules:
    /// K1 — every name × every document; K2 — every distinct last name × every document;
    /// K3, K4 — every document (K3 only for documents with an issue date); K5, K6 — scalar.
    /// </summary>
    /// <param name="subject">Normalized subject data.</param>
    /// <returns>The distinct set of computed key values.</returns>
    public static IReadOnlyCollection<SearchKeyValue> Build(NormalizedSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var keys = new HashSet<SearchKeyValue>();

        foreach (var doc in subject.Documents)
        {
            foreach (var name in subject.Names)
            {
                // K1: full name + document
                keys.Add(Compute(SearchKeyType.K1, name.LastName, name.FirstName, name.MiddleName, doc.TypeCode, doc.Series, doc.Number));
            }

            if (subject.BirthDate is { } birthDate)
            {
                foreach (var lastName in subject.Names.Select(n => n.LastName).Distinct())
                {
                    // K2: last name + birth date + document
                    keys.Add(Compute(SearchKeyType.K2, lastName, PersonalDataNormalizer.FormatDate(birthDate), doc.TypeCode, doc.Series, doc.Number));
                }
            }

            if (subject.Inn is not null && doc.IssueDate is { } issueDate)
            {
                // K3: document series + number + issue date + INN
                keys.Add(Compute(SearchKeyType.K3, doc.Series, doc.Number, PersonalDataNormalizer.FormatDate(issueDate), subject.Inn));
            }

            if (subject.Snils is not null)
            {
                // K4: document series + number + SNILS
                keys.Add(Compute(SearchKeyType.K4, doc.Series, doc.Number, subject.Snils));
            }
        }

        if (subject.BirthDate is { } bd)
        {
            if (subject.Snils is not null)
            {
                // K5: birth date + SNILS
                keys.Add(Compute(SearchKeyType.K5, PersonalDataNormalizer.FormatDate(bd), subject.Snils));
            }

            if (subject.Inn is not null)
            {
                // K6: birth date + INN
                keys.Add(Compute(SearchKeyType.K6, PersonalDataNormalizer.FormatDate(bd), subject.Inn));
            }
        }

        return keys;
    }

    private static SearchKeyValue Compute(SearchKeyType type, params string[] fields)
    {
        var preimage = new StringBuilder();
        preimage.Append('K').Append((int)type);
        foreach (var field in fields)
        {
            preimage.Append(Separator).Append(field);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(preimage.ToString()));
        return new SearchKeyValue(type, hash);
    }
}

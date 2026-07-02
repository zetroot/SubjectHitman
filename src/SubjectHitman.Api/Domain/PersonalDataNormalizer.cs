using System.Text;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Normalization rules for subject personal data (technical spec, § 5.1–5.2).
/// Applied both to incoming requests and to data before persisting, so that stored values
/// and computed hashes are always comparable.
/// </summary>
public static class PersonalDataNormalizer
{
    /// <summary>Placeholder stored instead of an absent name component.</summary>
    public const string AbsentNameComponent = "-";

    private static readonly Dictionary<char, char> LatinToCyrillic = new()
    {
        ['A'] = 'А',
        ['B'] = 'В',
        ['C'] = 'С',
        ['E'] = 'Е',
        ['H'] = 'Н',
        ['K'] = 'К',
        ['M'] = 'М',
        ['O'] = 'О',
        ['P'] = 'Р',
        ['T'] = 'Т',
        ['X'] = 'Х',
        ['Y'] = 'У',
    };

    /// <summary>
    /// Normalizes a full-name component: NFC, trim, whitespace collapse, upper-case,
    /// latin-to-cyrillic transliteration of visually identical letters (5791-U, Appendix 2, note 2).
    /// Absent or empty values become <see cref="AbsentNameComponent"/>.
    /// </summary>
    /// <param name="value">Raw name component; may be <see langword="null"/>.</param>
    /// <returns>The normalized component, never null or empty.</returns>
    public static string NormalizeNameComponent(string? value)
    {
        var basic = NormalizeBasic(value);
        if (basic.Length == 0)
        {
            return AbsentNameComponent;
        }

        var chars = basic.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (LatinToCyrillic.TryGetValue(chars[i], out var cyr))
            {
                chars[i] = cyr;
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Normalizes a document field (type code, series, number): NFC, trim, whitespace collapse, upper-case.
    /// Absent values become an empty string (an absent series matches an absent series —
    /// 5791-U, Appendix 2, note 4).
    /// </summary>
    /// <param name="value">Raw field value; may be <see langword="null"/>.</param>
    /// <returns>The normalized value; empty string when absent.</returns>
    public static string NormalizeDocumentField(string? value) => NormalizeBasic(value);

    /// <summary>
    /// Normalizes an INN or SNILS: strips every non-digit character (spec decision D2).
    /// The literal value <c>"-"</c>, an empty string, or a value with no digits denotes an absent indicator.
    /// </summary>
    /// <param name="value">Raw INN/SNILS value; may be <see langword="null"/>.</param>
    /// <returns>Digits-only string, or <see langword="null"/> when the indicator is absent.</returns>
    public static string? NormalizeDigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
        }

        return digits.Length == 0 ? null : digits.ToString();
    }

    /// <summary>
    /// Serializes a date to the canonical <c>yyyy-MM-dd</c> form used in key preimages.
    /// </summary>
    /// <param name="date">The date to serialize.</param>
    /// <returns>The canonical string representation.</returns>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static string NormalizeBasic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var nfc = value.Normalize(NormalizationForm.FormC).Trim();
        var sb = new StringBuilder(nfc.Length);
        var pendingSpace = false;
        foreach (var c in nfc)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}

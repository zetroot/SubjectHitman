namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Types of subject search keys, derived from Appendix 2 of Bank of Russia Directive 5791-U
/// after merging the current/previous data set pairs. Lower numeric value = stronger key.
/// </summary>
public enum SearchKeyType : short
{
    /// <summary>Full name + document type code, series and number (5791-U sets 1.1 + 1.2).</summary>
    K1 = 1,

    /// <summary>Last name + birth date + document type code, series and number (sets 1.3 + 1.4).</summary>
    K2 = 2,

    /// <summary>Document series, number, issue date + INN (sets 1.5 + 1.6).</summary>
    K3 = 3,

    /// <summary>Document series, number + SNILS (sets 1.7 + 1.8).</summary>
    K4 = 4,

    /// <summary>Birth date + SNILS (set 1.11).</summary>
    K5 = 5,

    /// <summary>Birth date + INN (set 1.12).</summary>
    K6 = 6,
}

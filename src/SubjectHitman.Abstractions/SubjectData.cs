namespace SubjectHitman.Abstractions;

/// <summary>
/// Personal data of a credit history subject (a citizen of the Russian Federation)
/// in the scope required by the search keys derived from Bank of Russia Directive 5791-U.
/// </summary>
/// <param name="LastName">Current last name. Required.</param>
/// <param name="FirstName">Current first name. Required.</param>
/// <param name="MiddleName">Current middle name / patronymic. Optional.</param>
/// <param name="BirthDate">Date of birth. Required (per Appendix 1 of Directive 5791-U).</param>
/// <param name="Document">Current identity document. Required, must contain an issue date.</param>
/// <param name="PreviousName">Previous full name, if the subject changed it. Optional.</param>
/// <param name="PreviousDocument">Previous identity document. Optional.</param>
/// <param name="Inn">
/// Taxpayer identification number (ИНН). Optional. The literal value <c>"-"</c> or an empty string
/// denotes an absent value.
/// </param>
/// <param name="Snils">
/// Individual insurance account number (СНИЛС). Optional. The literal value <c>"-"</c> or an empty string
/// denotes an absent value.
/// </param>
public record SubjectData(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly BirthDate,
    IdentityDocumentData Document,
    PersonNameData? PreviousName,
    IdentityDocumentData? PreviousDocument,
    string? Inn,
    string? Snils);

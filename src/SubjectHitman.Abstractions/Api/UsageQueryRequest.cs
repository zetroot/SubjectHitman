namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Request body of <c>POST /api/v1/free-reports/usage-query</c>:
/// personal data of a credit history subject taken from a credit report request.
/// </summary>
/// <param name="LastName">Current last name. Required.</param>
/// <param name="FirstName">Current first name. Required.</param>
/// <param name="MiddleName">Current middle name / patronymic. Optional.</param>
/// <param name="BirthDate">Date of birth. Required.</param>
/// <param name="Document">Current identity document. Required; <see cref="IdentityDocumentData.IssueDate"/> is required.</param>
/// <param name="PreviousName">Previous full name. Optional.</param>
/// <param name="PreviousDocument">Previous identity document. Optional; issue date is optional.</param>
/// <param name="Inn">Taxpayer number (ИНН). Optional; <c>"-"</c> or empty string means absent.</param>
/// <param name="Snils">Insurance account number (СНИЛС). Optional; <c>"-"</c> or empty string means absent.</param>
public record UsageQueryRequest(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly BirthDate,
    IdentityDocumentData Document,
    PersonNameData? PreviousName,
    IdentityDocumentData? PreviousDocument,
    string? Inn,
    string? Snils)
{
    /// <summary>
    /// Converts the request into the transport-agnostic <see cref="SubjectData"/> contract.
    /// </summary>
    /// <returns>Subject personal data equivalent to this request.</returns>
    public SubjectData ToSubjectData() => new(
        LastName, FirstName, MiddleName, BirthDate, Document, PreviousName, PreviousDocument, Inn, Snils);
}

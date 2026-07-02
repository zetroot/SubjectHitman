namespace SubjectHitman.Abstractions;

/// <summary>
/// Identity document (ДУЛ — документ, удостоверяющий личность) of a credit history subject
/// as received from an external system.
/// </summary>
/// <param name="TypeCode">Document type code per the Bank of Russia classifier. Required.</param>
/// <param name="Series">Document series. Optional: some document types have no series.</param>
/// <param name="Number">Document number. Required.</param>
/// <param name="IssueDate">
/// Document issue date. Required for the current document of the subject;
/// may be absent for a previous document (see technical spec, decision D1).
/// </param>
public record IdentityDocumentData(string TypeCode, string? Series, string Number, DateOnly? IssueDate);

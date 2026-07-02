namespace SubjectHitman.Abstractions.Api;

/// <summary>
/// Тело запроса <c>POST /api/v1/free-reports/usage-query</c>:
/// персональные данные субъекта кредитной истории из запроса кредитного отчёта.
/// </summary>
/// <param name="LastName">Текущая фамилия. Обязательное поле.</param>
/// <param name="FirstName">Текущее имя. Обязательное поле.</param>
/// <param name="MiddleName">Текущее отчество. Необязательное поле.</param>
/// <param name="BirthDate">Дата рождения. Обязательное поле.</param>
/// <param name="Document">Текущий документ, удостоверяющий личность. Обязателен; <see cref="IdentityDocumentData.IssueDate"/> обязательна.</param>
/// <param name="PreviousName">Предыдущее ФИО. Необязательное поле.</param>
/// <param name="PreviousDocument">Предыдущий документ, удостоверяющий личность. Необязателен; дата выдачи необязательна.</param>
/// <param name="Inn">ИНН. Необязательное поле; <c>"-"</c> или пустая строка означают отсутствие.</param>
/// <param name="Snils">СНИЛС. Необязательное поле; <c>"-"</c> или пустая строка означают отсутствие.</param>
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
    /// Преобразует запрос в транспортно-независимый контракт <see cref="SubjectData"/>.
    /// </summary>
    /// <returns>Персональные данные субъекта, эквивалентные этому запросу.</returns>
    public SubjectData ToSubjectData() => new(
        LastName, FirstName, MiddleName, BirthDate, Document, PreviousName, PreviousDocument, Inn, Snils);
}

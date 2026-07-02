namespace SubjectHitman.Abstractions;

/// <summary>
/// Персональные данные субъекта кредитной истории (гражданина РФ)
/// в объёме, необходимом для ключей поиска, производных от указания Банка России 5791-У.
/// </summary>
/// <param name="LastName">Текущая фамилия. Обязательное поле.</param>
/// <param name="FirstName">Текущее имя. Обязательное поле.</param>
/// <param name="MiddleName">Текущее отчество. Необязательное поле.</param>
/// <param name="BirthDate">Дата рождения. Обязательное поле (согласно приложению 1 к указанию 5791-У).</param>
/// <param name="Document">Текущий документ, удостоверяющий личность. Обязателен, должен содержать дату выдачи.</param>
/// <param name="PreviousName">Предыдущее ФИО, если субъект его менял. Необязательное поле.</param>
/// <param name="PreviousDocument">Предыдущий документ, удостоверяющий личность. Необязательное поле.</param>
/// <param name="Inn">
/// Идентификационный номер налогоплательщика (ИНН). Необязательное поле. Значение <c>"-"</c> или пустая строка
/// означают отсутствие показателя.
/// </param>
/// <param name="Snils">
/// Страховой номер индивидуального лицевого счёта (СНИЛС). Необязательное поле. Значение <c>"-"</c> или пустая строка
/// означают отсутствие показателя.
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

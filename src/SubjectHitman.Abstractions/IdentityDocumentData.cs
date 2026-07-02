namespace SubjectHitman.Abstractions;

/// <summary>
/// Документ, удостоверяющий личность (ДУЛ) субъекта кредитной истории,
/// полученный от внешней системы.
/// </summary>
/// <param name="TypeCode">Код вида документа по классификатору Банка России. Обязательное поле.</param>
/// <param name="Series">Серия документа. Необязательное поле: у некоторых видов документов серии нет.</param>
/// <param name="Number">Номер документа. Обязательное поле.</param>
/// <param name="IssueDate">
/// Дата выдачи документа. Обязательна для текущего документа субъекта;
/// может отсутствовать для предыдущего документа (см. техническую спецификацию, решение D1).
/// </param>
public record IdentityDocumentData(string TypeCode, string? Series, string Number, DateOnly? IssueDate);

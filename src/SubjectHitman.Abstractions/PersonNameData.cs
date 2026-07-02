namespace SubjectHitman.Abstractions;

/// <summary>
/// ФИО (фамилия, имя, отчество) субъекта кредитной истории, полученное от внешней системы.
/// </summary>
/// <param name="LastName">Фамилия. Обязательное поле.</param>
/// <param name="FirstName">Имя. Обязательное поле.</param>
/// <param name="MiddleName">Отчество. Необязательное поле.</param>
public record PersonNameData(string LastName, string FirstName, string? MiddleName);

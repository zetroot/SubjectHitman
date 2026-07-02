namespace SubjectHitman.Abstractions;

/// <summary>
/// Full name (last, first, middle) of a credit history subject as received from an external system.
/// </summary>
/// <param name="LastName">Last name (фамилия). Required.</param>
/// <param name="FirstName">First name (имя). Required.</param>
/// <param name="MiddleName">Middle name / patronymic (отчество). Optional.</param>
public record PersonNameData(string LastName, string FirstName, string? MiddleName);

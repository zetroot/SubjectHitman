using FluentValidation;
using SubjectHitman.Abstractions;

namespace SubjectHitman.Api.Endpoints;

/// <summary>
/// Валидатор ФИО (<see cref="PersonNameData"/>). Обязательны фамилия и имя.
/// </summary>
public class PersonNameValidator : AbstractValidator<PersonNameData>
{
    /// <summary>
    /// Регистрирует правила валидации компонентов ФИО.
    /// </summary>
    public PersonNameValidator()
    {
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");
    }
}

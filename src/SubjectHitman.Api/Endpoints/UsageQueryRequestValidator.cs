using FluentValidation;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Api.Domain;

namespace SubjectHitman.Api.Endpoints;

/// <summary>
/// Валидатор запроса <see cref="UsageQueryRequest"/> (техническая спецификация § 4.2).
/// Делегирует валидацию ФИО и ДУЛ вложенным валидаторам.
/// </summary>
public class UsageQueryRequestValidator : AbstractValidator<UsageQueryRequest>
{
    /// <summary>
    /// Регистрирует правила валидации для всех полей запроса.
    /// </summary>
    public UsageQueryRequestValidator()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1).Date);
        var personNameValidator = new PersonNameValidator();
        var documentValidator = new IdentityDocumentValidator();

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.BirthDate)
            .LessThan(today).WithMessage("Birth date must be in the past.");

        RuleFor(x => x.Document)
            .NotNull().WithMessage("Current identity document is required.")
            .SetValidator(documentValidator!);

        When(x => x.Document is not null, () =>
        {
            RuleFor(x => x.Document!.IssueDate)
                .NotNull().WithMessage("Issue date of the current document is required.");

            When(x => x.Document!.IssueDate is not null, () =>
            {
                RuleFor(x => x.Document!.IssueDate!.Value)
                    .LessThan(today).WithMessage("Issue date must be in the past.");
            });
        });

        When(x => x.PreviousName is not null, () =>
        {
            RuleFor(x => x.PreviousName!)
                .SetValidator(personNameValidator);
        });

        When(x => x.PreviousDocument is not null, () =>
        {
            RuleFor(x => x.PreviousDocument!)
                .SetValidator(documentValidator);
        });

        RuleFor(x => x.Inn)
            .Must(BeNullOrExactDigits(12)).WithMessage("Must contain exactly 12 digits when provided.");

        RuleFor(x => x.Snils)
            .Must(BeNullOrExactDigits(11)).WithMessage("Must contain exactly 11 digits when provided.");
    }

    private static Func<string?, bool> BeNullOrExactDigits(int expectedLength) =>
        value =>
        {
            var digits = PersonalDataNormalizer.NormalizeDigitsOnly(value);
            return digits is null || digits.Length == expectedLength;
        };
}

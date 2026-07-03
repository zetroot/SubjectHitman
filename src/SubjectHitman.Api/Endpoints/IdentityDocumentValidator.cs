using FluentValidation;
using SubjectHitman.Abstractions;

namespace SubjectHitman.Api.Endpoints;

/// <summary>
/// Валидатор документа, удостоверяющего личность (<see cref="IdentityDocumentData"/>).
/// Обязательны код типа и номер документа. Дата выдачи контекстно-зависима —
/// проверяется в корневом валидаторе запроса.
/// </summary>
public class IdentityDocumentValidator : AbstractValidator<IdentityDocumentData>
{
    /// <summary>
    /// Регистрирует правила валидации документа.
    /// </summary>
    public IdentityDocumentValidator()
    {
        RuleFor(x => x.TypeCode)
            .NotEmpty().WithMessage("Document type code is required.");

        RuleFor(x => x.Number)
            .NotEmpty().WithMessage("Document number is required.");
    }
}

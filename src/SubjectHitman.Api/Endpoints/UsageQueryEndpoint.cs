using Microsoft.AspNetCore.Http.HttpResults;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Api.Domain;
using Wolverine.Http;

namespace SubjectHitman.Api.Endpoints;

/// <summary>
/// HTTP-эндпоинт, отвечающий на запрос о количестве использованных бесплатных кредитных отчётов
/// субъектом в текущем календарном году (US-1).
/// </summary>
public static class UsageQueryEndpoint
{
    /// <summary>
    /// Идентифицирует субъекта по персональным данным из запроса кредитного отчёта
    /// (создавая или обогащая запись субъекта) и возвращает количество
    /// платных бесплатных отчётов с учётом кулдауна за текущий календарный год.
    /// </summary>
    /// <param name="request">Персональные данные субъекта из запроса кредитного отчёта.</param>
    /// <param name="identification">Сервис идентификации субъекта.</param>
    /// <param name="counter">Счётчик бесплатных отчётов.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns><c>200 OK</c> с количеством использованных отчётов или <c>400</c> с ошибками валидации.</returns>
    [WolverinePost("/api/v1/free-reports/usage-query")]
    public static async Task<Results<Ok<UsageQueryResponse>, ValidationProblem>> Post(
        UsageQueryRequest request,
        SubjectIdentificationService identification,
        FreeReportCounter counter,
        CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var subjectId = await identification.IdentifyAsync(request.ToSubjectData(), ct);
        var result = await counter.CountAsync(subjectId, ct);

        return TypedResults.Ok(new UsageQueryResponse(
            subjectId,
            result.UsedFreeReportsCount,
            result.PeriodStart,
            result.PeriodEnd));
    }

    /// <summary>
    /// Валидирует запрос согласно технической спецификации § 4.2.
    /// </summary>
    /// <param name="request">Запрос для валидации.</param>
    /// <returns>Ошибки валидации, сгруппированные по имени поля; пустой словарь, если запрос корректен.</returns>
    public static Dictionary<string, string[]> Validate(UsageQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1).Date);

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors["lastName"] = ["Last name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors["firstName"] = ["First name is required."];
        }

        if (request.BirthDate >= today)
        {
            errors["birthDate"] = ["Birth date must be in the past."];
        }

        if (request.Document is null)
        {
            errors["document"] = ["Current identity document is required."];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Document.TypeCode))
            {
                errors["document.typeCode"] = ["Document type code is required."];
            }

            if (string.IsNullOrWhiteSpace(request.Document.Number))
            {
                errors["document.number"] = ["Document number is required."];
            }

            if (request.Document.IssueDate is null)
            {
                errors["document.issueDate"] = ["Issue date of the current document is required."];
            }
            else if (request.Document.IssueDate >= today)
            {
                errors["document.issueDate"] = ["Issue date must be in the past."];
            }
        }

        if (request.PreviousName is { } prevName)
        {
            if (string.IsNullOrWhiteSpace(prevName.LastName))
            {
                errors["previousName.lastName"] = ["Previous last name is required when previousName is provided."];
            }

            if (string.IsNullOrWhiteSpace(prevName.FirstName))
            {
                errors["previousName.firstName"] = ["Previous first name is required when previousName is provided."];
            }
        }

        if (request.PreviousDocument is { } prevDoc)
        {
            if (string.IsNullOrWhiteSpace(prevDoc.TypeCode))
            {
                errors["previousDocument.typeCode"] = ["Document type code is required when previousDocument is provided."];
            }

            if (string.IsNullOrWhiteSpace(prevDoc.Number))
            {
                errors["previousDocument.number"] = ["Document number is required when previousDocument is provided."];
            }
        }

        ValidateDigits(errors, "inn", request.Inn, 12);
        ValidateDigits(errors, "snils", request.Snils, 11);

        return errors;
    }

    private static void ValidateDigits(Dictionary<string, string[]> errors, string field, string? value, int expectedLength)
    {
        var digits = PersonalDataNormalizer.NormalizeDigitsOnly(value);
        if (digits is not null && digits.Length != expectedLength)
        {
            errors[field] = [$"Must contain exactly {expectedLength} digits when provided."];
        }
    }
}

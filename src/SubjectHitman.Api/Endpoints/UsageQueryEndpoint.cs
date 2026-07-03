using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Domain.Counting;
using SubjectHitman.Domain.Identification;
using Wolverine.Http;

namespace SubjectHitman.Api.Endpoints;

/// <summary>
/// HTTP-эндпоинт, отвечающий на запрос о количестве использованных бесплатных кредитных отчётов
/// субъектом в текущем календарном году (US-1).
/// </summary>
public class UsageQueryEndpoint
{
    private readonly SubjectIdentificationService _identification;
    private readonly FreeReportCounter _counter;
    private readonly IValidator<UsageQueryRequest> _validator;

    /// <summary>
    /// Инициализирует экземпляр эндпоинта с переданными сервисами.
    /// </summary>
    /// <param name="identification">Сервис идентификации субъекта.</param>
    /// <param name="counter">Счётчик бесплатных отчётов.</param>
    /// <param name="validator">FluentValidation валидатор запроса.</param>
    public UsageQueryEndpoint(
        SubjectIdentificationService identification,
        FreeReportCounter counter,
        IValidator<UsageQueryRequest> validator)
    {
        _identification = identification;
        _counter = counter;
        _validator = validator;
    }

    /// <summary>
    /// Идентифицирует субъекта по персональным данным из запроса кредитного отчёта
    /// (создавая или обогащая запись субъекта) и возвращает количество
    /// платных бесплатных отчётов с учётом кулдауна за текущий календарный год.
    /// </summary>
    /// <param name="request">Персональные данные субъекта из запроса кредитного отчёта.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns><c>200 OK</c> с количеством использованных отчётов или <c>400</c> с ошибками валидации.</returns>
    [WolverinePost("/api/v1/free-reports/usage-query")]
    public async Task<Results<Ok<UsageQueryResponse>, ValidationProblem>> Post(
        UsageQueryRequest request,
        CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var subjectId = await _identification.IdentifyAsync(request.ToSubjectData(), ct);
        var result = await _counter.CountAsync(subjectId, ct);

        return TypedResults.Ok(new UsageQueryResponse(
            subjectId,
            result.UsedFreeReportsCount,
            result.PeriodStart,
            result.PeriodEnd));
    }
}

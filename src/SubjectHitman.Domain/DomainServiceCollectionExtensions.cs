using Microsoft.Extensions.DependencyInjection;
using SubjectHitman.Domain.Counting;
using SubjectHitman.Domain.Identification;

namespace SubjectHitman.Domain;

/// <summary>
/// Методы расширения для регистрации доменных сервисов в DI-контейнере.
/// </summary>
public static class DomainServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует доменные сервисы (идентификация субъекта, подсчёт бесплатных отчётов).
    /// </summary>
    /// <param name="services">Коллекция сервисов DI.</param>
    /// <returns>Та же коллекция сервисов для цепочки вызовов.</returns>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<SubjectIdentificationService>();
        services.AddScoped<FreeReportCounter>();
        return services;
    }
}

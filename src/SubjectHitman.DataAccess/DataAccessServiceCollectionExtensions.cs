using Microsoft.Extensions.DependencyInjection;
using SubjectHitman.DataAccess.Repositories;
using SubjectHitman.Domain.Repositories;

namespace SubjectHitman.DataAccess;

/// <summary>
/// Методы расширения для регистрации компонентов слоя доступа к данным в DI-контейнере.
/// </summary>
public static class DataAccessServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует реализации репозиториев слоя доступа к данным.
    /// Регистрация самого <see cref="AppDbContext"/> выполняется отдельно через
    /// <c>AddDbContextWithWolverineIntegration&lt;AppDbContext&gt;</c> в точке входа приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов DI.</param>
    /// <returns>Та же коллекция сервисов для цепочки вызовов.</returns>
    public static IServiceCollection AddDataAccess(this IServiceCollection services)
    {
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IReportUsageRepository, ReportUsageRepository>();
        return services;
    }
}

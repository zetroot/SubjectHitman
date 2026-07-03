using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SubjectHitman.DataAccess;

/// <summary>
/// Фабрика времени разработки для <see cref="AppDbContext"/>, используемая инструментарием
/// <c>dotnet ef</c> (создание миграций) без запуска полного хоста приложения.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=subject_hitman;Username=app;Password=app")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}

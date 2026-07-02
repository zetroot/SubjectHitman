using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SubjectHitman.Api.Infrastructure;

/// <summary>
/// Design-time factory for <see cref="AppDbContext"/> used by <c>dotnet ef</c> tooling
/// (migration generation) without booting the full application host.
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

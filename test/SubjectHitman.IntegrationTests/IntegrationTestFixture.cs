using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SubjectHitman.Abstractions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace SubjectHitman.IntegrationTests;

/// <summary>
/// Shared fixture: starts PostgreSQL and RabbitMQ containers and boots the application host
/// with a controllable in-memory report status stub.
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("subject_hitman")
        .WithUsername("app")
        .WithPassword("app")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .Build();

    /// <summary>The application factory bound to the containers.</summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Controllable status stub injected instead of the HTTP status client.</summary>
    public StubReportStatusClient StatusStub { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
                builder.UseSetting("ConnectionStrings:RabbitMq", _rabbitMq.GetConnectionString());
                builder.UseSetting("Saga:Timeout", "00:00:02");
                builder.UseSetting("Saga:MaxTimeoutRetries", "2");
                builder.UseSetting("FreeReports:CooldownPeriod", "1.00:00:00");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IReportStatusClient>();
                    services.AddSingleton<IReportStatusClient>(StatusStub);
                });
            });

        // Force host creation (runs migrations and Wolverine startup).
        _ = Factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}

/// <summary>
/// In-memory <see cref="IReportStatusClient"/> stub with per-report programmable statuses.
/// </summary>
public sealed class StubReportStatusClient : IReportStatusClient
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ReportStatus> _statuses = new();

    /// <summary>Sets the status the stub returns for the given report.</summary>
    /// <param name="reportId">Report identifier.</param>
    /// <param name="status">Status to return.</param>
    public void SetStatus(Guid reportId, ReportStatus status) => _statuses[reportId] = status;

    /// <inheritdoc />
    public Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct)
        => Task.FromResult(_statuses.GetValueOrDefault(reportId, ReportStatus.Unknown));
}

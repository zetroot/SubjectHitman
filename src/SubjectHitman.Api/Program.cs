using Microsoft.EntityFrameworkCore;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Domain;
using SubjectHitman.Api.Infrastructure;
using SubjectHitman.Api.Sagas;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Connection string 'RabbitMq' is not configured.");

builder.Services.AddOptions<FreeReportsOptions>()
    .BindConfiguration(FreeReportsOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(
        o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _),
        "FreeReports:TimeZone must be a valid IANA time zone identifier.")
    .ValidateOnStart();

builder.Services.AddOptions<SagaOptions>()
    .BindConfiguration(SagaOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ReportStatusApiOptions>()
    .BindConfiguration(ReportStatusApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
    (services, options) => options
        .UseNpgsql(postgresConnectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<SubjectIdentificationService>();
builder.Services.AddScoped<FreeReportCounter>();

builder.Services.AddHttpClient<IReportStatusClient, ReportStatusClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReportStatusApiOptions>>().Value;
    client.BaseAddress = options.BaseUrl;
    client.Timeout = options.RequestTimeout;
});

builder.Host.UseWolverine(opts =>
{
    opts.PersistMessagesWithPostgresql(postgresConnectionString);
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.UseDurableInboxOnAllListeners();

    opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();

    opts.ListenToRabbitQueue("subject-hitman.report-events", queue =>
    {
        queue.BindExchange("report-processing", "report.ordered");
        queue.BindExchange("report-processing", "report.completed");
        queue.BindExchange("report-processing", "report.failed");
    });

    opts.OnException<Exception>()
        .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
        .Then.MoveToErrorQueue();
});

builder.Services.AddProblemDetails();
builder.Services.AddWolverineHttp();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapWolverineEndpoints();
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

/// <summary>
/// Application entry point marker class enabling <c>WebApplicationFactory</c>-based integration tests.
/// </summary>
public partial class Program
{
    /// <summary>Protected constructor for the generated entry-point class.</summary>
    protected Program()
    {
    }
}

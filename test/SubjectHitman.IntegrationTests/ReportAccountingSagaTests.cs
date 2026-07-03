using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Messages;
using SubjectHitman.Domain.Entities;
using SubjectHitman.DataAccess;
using Wolverine;

namespace SubjectHitman.IntegrationTests;

[Collection(nameof(AppCollection))]
public class ReportAccountingSagaTests(IntegrationTestFixture fixture)
{
    private static SubjectData NewSubject() => new(
        LastName: "Сидоров",
        FirstName: "Пётр",
        MiddleName: null,
        BirthDate: new DateOnly(1985, 6, 1),
        Document: new IdentityDocumentData("21", "4000", Guid.NewGuid().ToString("N")[..6], new DateOnly(2005, 3, 10)),
        PreviousName: null,
        PreviousDocument: null,
        Inn: null,
        Snils: null);

    private async Task<ReportUsage?> WaitForStatusAsync(Guid reportId, ReportUsageStatus expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usage = await db.ReportUsages.AsNoTracking()
                .SingleOrDefaultAsync(r => r.ReportId == reportId, TestContext.Current.CancellationToken);
            if (usage is not null && usage.Status == expected)
            {
                return usage;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        return null;
    }

    private IMessageBus Bus()
        => fixture.Factory.Services.GetRequiredService<IMessageBus>();

    [Fact]
    public async Task ReportOrdered_ThenCompleted_AccountsAsCharged()
    {
        var reportId = Guid.NewGuid();
        var bus = Bus();

        await bus.InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportCompleted(reportId, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(10));
        usage.ShouldNotBeNull();
        usage.IsFree.ShouldBeTrue();
        usage.FinishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReportOrdered_ThenFailed_AccountsAsNotCharged()
    {
        var reportId = Guid.NewGuid();
        var bus = Bus();

        await bus.InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportFailed(reportId, DateTimeOffset.UtcNow, "processing error"), TestContext.Current.CancellationToken);

        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.NotCharged, TimeSpan.FromSeconds(10));
        usage.ShouldNotBeNull();
    }

    [Fact]
    public async Task DuplicateReportOrdered_DoesNotDuplicateUsage()
    {
        var reportId = Guid.NewGuid();
        var subject = NewSubject();
        var bus = Bus();

        await bus.InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, subject), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportCompleted(reportId, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(10));
        usage.ShouldNotBeNull();

        // A late duplicate ReportOrdered may start a new saga instance, but the usage record
        // is keyed by reportId: status and subject must stay unchanged.
        await bus.InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, subject), TestContext.Current.CancellationToken);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await db.ReportUsages.AsNoTracking()
            .SingleAsync(r => r.ReportId == reportId, TestContext.Current.CancellationToken);
        after.Status.ShouldBe(ReportUsageStatus.Charged);
        after.SubjectId.ShouldBe(usage.SubjectId);
    }

    [Fact]
    public async Task OrphanCompleted_IsDiscardedWithoutError()
    {
        // Q2: ReportCompleted for an unknown reportId must be acked, not throw.
        await Bus().InvokeAsync(new ReportCompleted(Guid.NewGuid(), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Timeout_StatusSuccess_AccountsAsCharged()
    {
        var reportId = Guid.NewGuid();
        fixture.StatusStub.SetStatus(reportId, ReportStatus.Success);

        await Bus().InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);

        // Saga:Timeout is 2s in the fixture; the scheduled check resolves via the stub.
        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(30));
        usage.ShouldNotBeNull();
    }

    [Fact]
    public async Task Timeout_StatusUnknown_RetriesExhausted_AccountsAsNotCharged()
    {
        var reportId = Guid.NewGuid();
        // Stub returns Unknown by default; MaxTimeoutRetries is 2 in the fixture.

        await Bus().InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);

        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.NotCharged, TimeSpan.FromSeconds(30));
        usage.ShouldNotBeNull();
    }

    [Fact]
    public async Task ChargedFreeReports_AppearInUsageCount()
    {
        var subject = NewSubject();
        var bus = Bus();

        // Two charged free reports 3 days apart (beyond the 1-day cooldown).
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await bus.InvokeAsync(new ReportOrdered(firstId, now.AddDays(-3), IsFree: true, subject), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportCompleted(firstId, now.AddDays(-3)), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportOrdered(secondId, now, IsFree: true, subject), TestContext.Current.CancellationToken);
        await bus.InvokeAsync(new ReportCompleted(secondId, now), TestContext.Current.CancellationToken);

        (await WaitForStatusAsync(firstId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(10))).ShouldNotBeNull();
        (await WaitForStatusAsync(secondId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(10))).ShouldNotBeNull();

        var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/free-reports/usage-query",
            new Abstractions.Api.UsageQueryRequest(
                subject.LastName, subject.FirstName, subject.MiddleName, subject.BirthDate,
                subject.Document, subject.PreviousName, subject.PreviousDocument, subject.Inn, subject.Snils),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<Abstractions.Api.UsageQueryResponse>(
            TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.UsedFreeReportsCount.ShouldBe(2);
    }

    /// <summary>
    /// US-3: зависший отчёт, статус-API возвращает Failed — учитывается как не списанный.
    /// </summary>
    [Fact]
    public async Task Timeout_StatusFailed_AccountsAsNotCharged()
    {
        var reportId = Guid.NewGuid();
        fixture.StatusStub.SetStatus(reportId, ReportStatus.Failed);

        await Bus().InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);

        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.NotCharged, TimeSpan.FromSeconds(30));
        usage.ShouldNotBeNull();
    }

    /// <summary>
    /// US-3: первая проверка через статус-API — Unknown, но вторая — Success.
    /// Отчёт списывается после перепланирования.
    /// </summary>
    [Fact]
    public async Task Timeout_UnknownThenSuccess_ResolvesOnRecheck()
    {
        var reportId = Guid.NewGuid();
        // Stub: Unknown by default for the first check.
        // After a delay, set it to Success so the recheck resolves.
        fixture.StatusStub.SetStatus(reportId, ReportStatus.Unknown);

        await Bus().InvokeAsync(new ReportOrdered(reportId, DateTimeOffset.UtcNow, IsFree: true, NewSubject()), TestContext.Current.CancellationToken);

        // Wait for the first check to fire (2s timeout + processing), then switch the stub.
        await Task.Delay(3_000, TestContext.Current.CancellationToken);
        fixture.StatusStub.SetStatus(reportId, ReportStatus.Success);

        var usage = await WaitForStatusAsync(reportId, ReportUsageStatus.Charged, TimeSpan.FromSeconds(30));
        usage.ShouldNotBeNull();
    }

    /// <summary>
    /// Q2: ReportFailed для неизвестного reportId подтверждается и отбрасывается.
    /// </summary>
    [Fact]
    public async Task OrphanFailed_IsDiscardedWithoutError()
    {
        await Bus().InvokeAsync(new ReportFailed(Guid.NewGuid(), DateTimeOffset.UtcNow, "stale event"), TestContext.Current.CancellationToken);
    }
}

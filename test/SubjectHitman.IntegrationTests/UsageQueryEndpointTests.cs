using System.Net;
using System.Net.Http.Json;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;

namespace SubjectHitman.IntegrationTests;

[Collection(nameof(AppCollection))]
public class UsageQueryEndpointTests(IntegrationTestFixture fixture)
{
    private static UsageQueryRequest NewRequest(string lastName = "Иванов", string number = "123456") => new(
        LastName: lastName,
        FirstName: "Иван",
        MiddleName: "Иванович",
        BirthDate: new DateOnly(1990, 1, 15),
        Document: new IdentityDocumentData("21", "4510", number, new DateOnly(2010, 5, 20)),
        PreviousName: null,
        PreviousDocument: null,
        Inn: null,
        Snils: null);

    [Fact]
    public async Task NewSubject_CreatedWithZeroCount()
    {
        var client = fixture.Factory.CreateClient();
        var request = NewRequest(number: Guid.NewGuid().ToString("N")[..6]);

        var response = await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.SubjectId.ShouldNotBe(Guid.Empty);
        body.UsedFreeReportsCount.ShouldBe(0);
    }

    [Fact]
    public async Task RepeatedRequest_SameSubjectId()
    {
        var client = fixture.Factory.CreateClient();
        var request = NewRequest(number: Guid.NewGuid().ToString("N")[..6]);

        var first = await (await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);
        var second = await (await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.SubjectId.ShouldBe(second.SubjectId);
    }

    [Fact]
    public async Task OverlappingData_IdentifiesSameSubject()
    {
        var client = fixture.Factory.CreateClient();
        var number = Guid.NewGuid().ToString("N")[..6];
        var snils = "98765432109";

        // First request: name + document + SNILS.
        var first = await (await client.PostAsJsonAsync(
                "/api/v1/free-reports/usage-query",
                NewRequest(number: number) with { Snils = snils },
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);

        // Second request: different (married) name, same document -> K1 differs, but K4 (doc+snils) matches.
        var second = await (await client.PostAsJsonAsync(
                "/api/v1/free-reports/usage-query",
                NewRequest(lastName: "Петрова", number: number) with { Snils = snils },
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.SubjectId.ShouldBe(second.SubjectId);
    }

    /// <summary>
    /// § 5.4: два субъекта с общим документом, но разными идентификаторами (INN vs SNILS)
    /// разрешаются корректно — каждый последующий запрос с теми же данными возвращает
    /// того же субъекта. Логика разрешения конфликта протестирована в ConflictResolutionTests.
    /// </summary>
    [Fact]
    public async Task TwoSubjectsWithSharedDocument_ResolveConsistently()
    {
        var client = fixture.Factory.CreateClient();
        var sharedDocNumber = Guid.NewGuid().ToString("N")[..6];
        var innValue = "500100732259";
        var snilsValue = "11223344595";

        var reqA = NewRequest(lastName: "Петров", number: sharedDocNumber) with { Inn = innValue };
        var idA = (await (await client.PostAsJsonAsync(
                "/api/v1/free-reports/usage-query", reqA, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken))!.SubjectId;

        var reqB = NewRequest(lastName: "Сидоров", number: sharedDocNumber) with { Snils = snilsValue };
        var idB = (await (await client.PostAsJsonAsync(
                "/api/v1/free-reports/usage-query", reqB, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken))!.SubjectId;

        idA.ShouldNotBe(idB);

        var recheckId = (await (await client.PostAsJsonAsync(
                "/api/v1/free-reports/usage-query", reqA, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken))!.SubjectId;
        recheckId.ShouldBe(idA);
    }

    [Fact]
    public async Task InvalidRequest_Returns400WithProblemDetails()
    {
        var client = fixture.Factory.CreateClient();
        var request = NewRequest() with { LastName = "" };

        var response = await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("lastName");
    }
}

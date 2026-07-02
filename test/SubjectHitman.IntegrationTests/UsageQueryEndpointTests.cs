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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.SubjectId);
        Assert.Equal(0, body.UsedFreeReportsCount);
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

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SubjectId, second.SubjectId);
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

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SubjectId, second.SubjectId);
    }

    [Fact]
    public async Task InvalidRequest_Returns400WithProblemDetails()
    {
        var client = fixture.Factory.CreateClient();
        var request = NewRequest() with { LastName = "" };

        var response = await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("lastName", body, StringComparison.OrdinalIgnoreCase);
    }
}

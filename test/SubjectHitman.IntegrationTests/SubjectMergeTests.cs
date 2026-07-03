using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.DataAccess;

namespace SubjectHitman.IntegrationTests;

/// <summary>
/// Тесты слияния персональных данных при повторной идентификации (требование Q1).
/// Проверяют, что повторный запрос для уже известного субъекта:
/// — пополняет отсутствующие поля (новое значение заполняет NULL);
/// — сохраняет хранимое значение при конфликте (не заменяет существующее);
/// — накапливает имена и документы, не дублируя;
/// — идемпотентен (повтор тех же данных не создаёт дубликатов).
/// </summary>
[Collection(nameof(AppCollection))]
public class SubjectMergeTests(IntegrationTestFixture fixture)
{
    private static UsageQueryRequest NewRequest(
        string lastName, string firstName, string number, string? inn = null, string? snils = null) => new(
        LastName: lastName,
        FirstName: firstName,
        MiddleName: null,
        BirthDate: new DateOnly(1985, 3, 10),
        Document: new IdentityDocumentData("21", "4501", number, new DateOnly(2015, 8, 1)),
        PreviousName: null,
        PreviousDocument: null,
        Inn: inn,
        Snils: snils);

    private async Task<Guid> IdentifyAsync(UsageQueryRequest request)
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/free-reports/usage-query", request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UsageQueryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.SubjectId.ShouldNotBe(Guid.Empty);
        return body.SubjectId;
    }

    /// <summary>
    /// Q1: при конфликте скалярного значения (ИНН различается) сохраняется хранимое.
    /// </summary>
    [Fact]
    public async Task ConflictingInn_KeepsStoredValue()
    {
        var number = Guid.NewGuid().ToString("N")[..6];
        var storedInn = "500100732259";
        var conflictingInn = "987654321098";

        var firstId = await IdentifyAsync(NewRequest("Иванов", "Пётр", number, inn: storedInn));
        var secondId = await IdentifyAsync(NewRequest("Иванов", "Пётр", number, inn: conflictingInn));

        firstId.ShouldBe(secondId);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await db.Subjects.SingleAsync(s => s.Id == firstId, TestContext.Current.CancellationToken);
        subject.Inn.ShouldBe(storedInn);
    }

    /// <summary>
    /// Q1: отсутствующее значение (NULL) заполняется из нового запроса.
    /// </summary>
    [Fact]
    public async Task MissingInn_FilledFromRequest()
    {
        var number = Guid.NewGuid().ToString("N")[..6];
        var newInn = "500100732259";

        var firstId = await IdentifyAsync(NewRequest("Кузнецов", "Николай", number, inn: null));
        var secondId = await IdentifyAsync(NewRequest("Кузнецов", "Николай", number, inn: newInn));

        firstId.ShouldBe(secondId);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await db.Subjects
            .Include(s => s.SearchKeys)
            .SingleAsync(s => s.Id == firstId, TestContext.Current.CancellationToken);
        subject.Inn.ShouldBe(newInn);
        // После появления ИНН должны появиться ключи K3 и K6.
        subject.SearchKeys.ShouldContain(k => k.KeyType == SubjectHitman.Domain.Entities.SearchKeyType.K3);
        subject.SearchKeys.ShouldContain(k => k.KeyType == SubjectHitman.Domain.Entities.SearchKeyType.K6);
    }

    /// <summary>
    /// § 5.5: новая фамилия при повторном запросе добавляется в историю, не заменяя старую.
    /// Поиск работает по обеим фамилиям. Матчинг идёт через документ + SNILS (K4).
    /// </summary>
    [Fact]
    public async Task NewLastName_AppendedToHistory()
    {
        var number = Guid.NewGuid().ToString("N")[..6];
        var snils = "11223344595";

        var firstId = await IdentifyAsync(NewRequest("Смирнова", "Анна", number, snils: snils));
        var secondId = await IdentifyAsync(NewRequest("Иванова", "Анна", number, snils: snils));

        firstId.ShouldBe(secondId);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await db.Subjects
            .Include(s => s.Names)
            .SingleAsync(s => s.Id == firstId, TestContext.Current.CancellationToken);
        subject.Names.ShouldContain(n => n.LastName == "СМИРНОВА");
        subject.Names.ShouldContain(n => n.LastName == "ИВАНОВА");
        subject.Names.Count.ShouldBe(2);

        // Третий запрос по первой фамилии идентифицирует того же субъекта.
        var thirdId = await IdentifyAsync(NewRequest("Смирнова", "Анна", number, snils: snils));
        thirdId.ShouldBe(firstId);
    }

    /// <summary>
    /// Повторный запрос тех же данных не создаёт дубликатов имён и документов.
    /// </summary>
    [Fact]
    public async Task SameDataTwice_NoDuplicateNamesOrDocuments()
    {
        var number = Guid.NewGuid().ToString("N")[..6];

        var firstId = await IdentifyAsync(NewRequest("Петров", "Сергей", number));
        var secondId = await IdentifyAsync(NewRequest("Петров", "Сергей", number));

        firstId.ShouldBe(secondId);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await db.Subjects
            .Include(s => s.Names)
            .Include(s => s.Documents)
            .SingleAsync(s => s.Id == firstId, TestContext.Current.CancellationToken);
        subject.Names.ShouldHaveSingleItem();
        subject.Documents.ShouldHaveSingleItem();
    }
}

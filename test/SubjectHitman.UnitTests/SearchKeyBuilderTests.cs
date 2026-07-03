using SubjectHitman.Abstractions;
using SubjectHitman.Domain;
using SubjectHitman.Domain.Entities;
using SubjectHitman.Domain.Identification;

namespace SubjectHitman.UnitTests;

public class SearchKeyBuilderTests
{
    private static SubjectData FullData(
        string? inn = "500100732259",
        string? snils = "112-233-445 95",
        IdentityDocumentData? previousDocument = null,
        PersonNameData? previousName = null) => new(
        LastName: "Иванов",
        FirstName: "Иван",
        MiddleName: "Иванович",
        BirthDate: new DateOnly(1990, 1, 15),
        Document: new IdentityDocumentData("21", "4510", "123456", new DateOnly(2010, 5, 20)),
        PreviousName: previousName,
        PreviousDocument: previousDocument,
        Inn: inn,
        Snils: snils);

    private static IReadOnlyCollection<SearchKeyValue> Build(SubjectData data)
        => SearchKeyBuilder.Build(NormalizedSubject.FromSubjectData(data));

    [Fact]
    public void FullData_ProducesAllSixKeyTypes()
    {
        var keys = Build(FullData());
        keys.Select(k => k.KeyType).Distinct().OrderBy(t => t)
            .ShouldBe([SearchKeyType.K1, SearchKeyType.K2, SearchKeyType.K3, SearchKeyType.K4, SearchKeyType.K5, SearchKeyType.K6]);
        keys.Count.ShouldBe(6);
    }

    [Fact]
    public void NoInn_SkipsK3AndK6()
    {
        var keys = Build(FullData(inn: null));
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K3);
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K6);
    }

    [Fact]
    public void DashInn_TreatedAsAbsent()
    {
        var keys = Build(FullData(inn: "-"));
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K3);
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K6);
    }

    [Fact]
    public void NoSnils_SkipsK4AndK5()
    {
        var keys = Build(FullData(snils: null));
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K4);
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K5);
    }

    [Fact]
    public void PreviousDocumentWithoutIssueDate_ParticipatesInK1K2K4_ButNotK3()
    {
        var prevDoc = new IdentityDocumentData("21", "4501", "654321", IssueDate: null);
        var keys = Build(FullData(previousDocument: prevDoc));

        // Two documents -> two K1, two K2, two K4; only one K3 (prev doc has no issue date).
        keys.Count(k => k.KeyType == SearchKeyType.K1).ShouldBe(2);
        keys.Count(k => k.KeyType == SearchKeyType.K2).ShouldBe(2);
        keys.Count(k => k.KeyType == SearchKeyType.K4).ShouldBe(2);
        keys.Count(k => k.KeyType == SearchKeyType.K3).ShouldBe(1);
    }

    [Fact]
    public void PreviousName_MultipliesK1AndK2()
    {
        var keys = Build(FullData(previousName: new PersonNameData("Петров", "Иван", "Иванович")));
        keys.Count(k => k.KeyType == SearchKeyType.K1).ShouldBe(2); // 2 names x 1 doc
        keys.Count(k => k.KeyType == SearchKeyType.K2).ShouldBe(2); // 2 distinct last names x 1 doc
    }

    [Fact]
    public void SameLastNameInPreviousName_DoesNotDuplicateK2()
    {
        var keys = Build(FullData(previousName: new PersonNameData("Иванов", "Пётр", "Иванович")));
        keys.Count(k => k.KeyType == SearchKeyType.K1).ShouldBe(2); // different first names
        keys.Count(k => k.KeyType == SearchKeyType.K2).ShouldBe(1); // same last name -> one key
    }

    [Fact]
    public void SameDataDifferentFormatting_ProducesIdenticalHashes()
    {
        var canonical = Build(FullData());
        var messy = Build(new SubjectData(
            LastName: "  иванов ",
            FirstName: "ИВАН",
            MiddleName: "иванович",
            BirthDate: new DateOnly(1990, 1, 15),
            Document: new IdentityDocumentData(" 21", "4510 ", " 123456 ", new DateOnly(2010, 5, 20)),
            PreviousName: null,
            PreviousDocument: null,
            Inn: " 500-100-732-259 ",
            Snils: "11223344595"));

        canonical.OrderBy(k => k.KeyType).Select(k => Convert.ToHexString(k.Hash))
            .ShouldBe(messy.OrderBy(k => k.KeyType).Select(k => Convert.ToHexString(k.Hash)));
    }

    [Fact]
    public void LatinLookalikes_ProduceSameHashAsCyrillic()
    {
        // "Иванoв" with latin 'o' must hash identically to pure Cyrillic "Иванов".
        var latin = Build(FullData() with { LastName = "Иванoв" });
        var cyrillic = Build(FullData() with { LastName = "Иванов" });
        cyrillic.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h)
            .ShouldBe(latin.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
    }

    [Fact]
    public void MissingMiddleName_UsesDashPlaceholder()
    {
        var withNull = Build(FullData() with { MiddleName = null });
        var withDash = Build(FullData() with { MiddleName = "-" });
        withDash.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h)
            .ShouldBe(withNull.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
    }

    [Fact]
    public void MissingSeries_MatchesMissingSeries()
    {
        var a = Build(FullData() with { Document = new IdentityDocumentData("21", null, "123456", new DateOnly(2010, 5, 20)) });
        var b = Build(FullData() with { Document = new IdentityDocumentData("21", "", "123456", new DateOnly(2010, 5, 20)) });
        a.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h)
            .ShouldBe(b.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
    }

    [Fact]
    public void DifferentKeyTypes_NeverCollide()
    {
        // K5 (birthDate|snils) and K6 (birthDate|inn) with identical field values
        // must still differ thanks to the type prefix.
        var data = FullData(inn: "11223344595", snils: "11223344595");
        var keys = Build(data);
        var k5 = keys.Single(k => k.KeyType == SearchKeyType.K5);
        var k6 = keys.Single(k => k.KeyType == SearchKeyType.K6);
        Convert.ToHexString(k5.Hash).ShouldNotBe(Convert.ToHexString(k6.Hash));
    }

    [Fact]
    public void Hashes_Are32Bytes()
        => Build(FullData()).ShouldAllBe(k => k.Hash.Length == 32);

    /// <summary>
    /// § 5.3: хранимый субъект без даты рождения (edge case — миграция или ручная вставка)
    /// пропускает ключи K2, K5, K6. Тест через NormalizedSubject напрямую,
    /// так как API-контракт SubjectData требует BirthDate обязательным (D1).
    /// </summary>
    [Fact]
    public void StoredSubjectWithoutBirthDate_SkipsK2K5K6()
    {
        var subject = NormalizedSubject.FromStored(
            [new NormalizedName("ИВАНОВ", "ИВАН", "ИВАНОВИЧ")],
            [new NormalizedDocument("21", "4510", "123456", new DateOnly(2010, 5, 20))],
            birthDate: null,
            inn: "500100732259",
            snils: "11223344595");
        var keys = SearchKeyBuilder.Build(subject);

        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K2);
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K5);
        keys.ShouldNotContain(k => k.KeyType == SearchKeyType.K6);
        keys.ShouldContain(k => k.KeyType == SearchKeyType.K1);
        keys.ShouldContain(k => k.KeyType == SearchKeyType.K3);
        keys.ShouldContain(k => k.KeyType == SearchKeyType.K4);
    }
}

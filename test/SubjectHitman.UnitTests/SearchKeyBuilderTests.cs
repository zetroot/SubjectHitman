using SubjectHitman.Abstractions;
using SubjectHitman.Api.Domain;
using SubjectHitman.Domain;
using SubjectHitman.Domain.Entities;

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
        Assert.Equal(
            new[] { SearchKeyType.K1, SearchKeyType.K2, SearchKeyType.K3, SearchKeyType.K4, SearchKeyType.K5, SearchKeyType.K6 },
            keys.Select(k => k.KeyType).Distinct().OrderBy(t => t).ToArray());
        Assert.Equal(6, keys.Count);
    }

    [Fact]
    public void NoInn_SkipsK3AndK6()
    {
        var keys = Build(FullData(inn: null));
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K3);
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K6);
    }

    [Fact]
    public void DashInn_TreatedAsAbsent()
    {
        var keys = Build(FullData(inn: "-"));
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K3);
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K6);
    }

    [Fact]
    public void NoSnils_SkipsK4AndK5()
    {
        var keys = Build(FullData(snils: null));
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K4);
        Assert.DoesNotContain(keys, k => k.KeyType == SearchKeyType.K5);
    }

    [Fact]
    public void PreviousDocumentWithoutIssueDate_ParticipatesInK1K2K4_ButNotK3()
    {
        var prevDoc = new IdentityDocumentData("21", "4501", "654321", IssueDate: null);
        var keys = Build(FullData(previousDocument: prevDoc));

        // Two documents -> two K1, two K2, two K4; only one K3 (prev doc has no issue date).
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K1));
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K2));
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K4));
        Assert.Equal(1, keys.Count(k => k.KeyType == SearchKeyType.K3));
    }

    [Fact]
    public void PreviousName_MultipliesK1AndK2()
    {
        var keys = Build(FullData(previousName: new PersonNameData("Петров", "Иван", "Иванович")));
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K1)); // 2 names x 1 doc
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K2)); // 2 distinct last names x 1 doc
    }

    [Fact]
    public void SameLastNameInPreviousName_DoesNotDuplicateK2()
    {
        var keys = Build(FullData(previousName: new PersonNameData("Иванов", "Пётр", "Иванович")));
        Assert.Equal(2, keys.Count(k => k.KeyType == SearchKeyType.K1)); // different first names
        Assert.Equal(1, keys.Count(k => k.KeyType == SearchKeyType.K2)); // same last name -> one key
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

        Assert.Equal(
            canonical.OrderBy(k => k.KeyType).Select(k => Convert.ToHexString(k.Hash)),
            messy.OrderBy(k => k.KeyType).Select(k => Convert.ToHexString(k.Hash)));
    }

    [Fact]
    public void LatinLookalikes_ProduceSameHashAsCyrillic()
    {
        // "Иванoв" with latin 'o' must hash identically to pure Cyrillic "Иванов".
        var latin = Build(FullData() with { LastName = "Иванoв" });
        var cyrillic = Build(FullData() with { LastName = "Иванов" });
        Assert.Equal(
            cyrillic.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h),
            latin.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
    }

    [Fact]
    public void MissingMiddleName_UsesDashPlaceholder()
    {
        var withNull = Build(FullData() with { MiddleName = null });
        var withDash = Build(FullData() with { MiddleName = "-" });
        Assert.Equal(
            withDash.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h),
            withNull.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
    }

    [Fact]
    public void MissingSeries_MatchesMissingSeries()
    {
        var a = Build(FullData() with { Document = new IdentityDocumentData("21", null, "123456", new DateOnly(2010, 5, 20)) });
        var b = Build(FullData() with { Document = new IdentityDocumentData("21", "", "123456", new DateOnly(2010, 5, 20)) });
        Assert.Equal(
            a.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h),
            b.Select(k => Convert.ToHexString(k.Hash)).OrderBy(h => h));
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
        Assert.NotEqual(Convert.ToHexString(k5.Hash), Convert.ToHexString(k6.Hash));
    }

    [Fact]
    public void Hashes_Are32Bytes()
        => Assert.All(Build(FullData()), k => Assert.Equal(32, k.Hash.Length));
}

using SubjectHitman.Domain.Identification;

namespace SubjectHitman.UnitTests;

public class PersonalDataNormalizerTests
{
    [Theory]
    [InlineData("Иванов", "ИВАНОВ")]
    [InlineData("  иванов  ", "ИВАНОВ")]
    [InlineData("Иванов   Петров", "ИВАНОВ ПЕТРОВ")]
    public void NormalizeNameComponent_TrimsCollapsesAndUppercases(string input, string expected)
        => Assert.Equal(expected, PersonalDataNormalizer.NormalizeNameComponent(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeNameComponent_AbsentValue_ReturnsDash(string? input)
        => Assert.Equal("-", PersonalDataNormalizer.NormalizeNameComponent(input));

    [Theory]
    // Latin letters visually identical to Cyrillic are transliterated (5791-U note 2).
    [InlineData("ABCEHKMOPTXY", "АВСЕНКМОРТХУ")]
    [InlineData("Иванoва", "ИВАНОВА")] // latin 'o' inside a Cyrillic word
    [InlineData("Cмирнов", "СМИРНОВ")] // latin 'C' at the start
    public void NormalizeNameComponent_TransliteratesLatinLookalikes(string input, string expected)
        => Assert.Equal(expected, PersonalDataNormalizer.NormalizeNameComponent(input));

    [Fact]
    public void NormalizeNameComponent_KeepsNonLookalikeLatinAsIs()
        // S, I, D, N, G, R, W, U, Z, F, L, Q are not visually identical to Cyrillic and stay latin.
        => Assert.Equal("SIDNGRWUZFLQ", PersonalDataNormalizer.NormalizeNameComponent("sidngrwuzflq"));

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" 45 10 ", "45 10")]
    [InlineData("iv-ab", "IV-AB")]
    public void NormalizeDocumentField_Normalizes(string? input, string expected)
        => Assert.Equal(expected, PersonalDataNormalizer.NormalizeDocumentField(input));

    [Theory]
    [InlineData("500100732259", "500100732259")]
    [InlineData("112-233-445 95", "11223344595")]
    [InlineData("-", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void NormalizeDigitsOnly_StripsNonDigits_AbsentReturnsNull(string? input, string? expected)
        => Assert.Equal(expected, PersonalDataNormalizer.NormalizeDigitsOnly(input));

    [Fact]
    public void FormatDate_UsesCanonicalForm()
        => Assert.Equal("1990-01-05", PersonalDataNormalizer.FormatDate(new DateOnly(1990, 1, 5)));
}

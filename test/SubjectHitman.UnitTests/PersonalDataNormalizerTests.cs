using SubjectHitman.Domain.Identification;

namespace SubjectHitman.UnitTests;

public class PersonalDataNormalizerTests
{
    [Theory]
    [InlineData("Иванов", "ИВАНОВ")]
    [InlineData("  иванов  ", "ИВАНОВ")]
    [InlineData("Иванов   Петров", "ИВАНОВ ПЕТРОВ")]
    public void NormalizeNameComponent_TrimsCollapsesAndUppercases(string input, string expected)
        => PersonalDataNormalizer.NormalizeNameComponent(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeNameComponent_AbsentValue_ReturnsDash(string? input)
        => PersonalDataNormalizer.NormalizeNameComponent(input).ShouldBe("-");

    [Theory]
    // Latin letters visually identical to Cyrillic are transliterated (5791-U note 2).
    [InlineData("ABCEHKMOPTXY", "АВСЕНКМОРТХУ")]
    [InlineData("Иванoва", "ИВАНОВА")] // latin 'o' inside a Cyrillic word
    [InlineData("Cмирнов", "СМИРНОВ")] // latin 'C' at the start
    public void NormalizeNameComponent_TransliteratesLatinLookalikes(string input, string expected)
        => PersonalDataNormalizer.NormalizeNameComponent(input).ShouldBe(expected);

    [Fact]
    public void NormalizeNameComponent_KeepsNonLookalikeLatinAsIs()
        // S, I, D, N, G, R, W, U, Z, F, L, Q are not visually identical to Cyrillic and stay latin.
        => PersonalDataNormalizer.NormalizeNameComponent("sidngrwuzflq").ShouldBe("SIDNGRWUZFLQ");

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" 45 10 ", "45 10")]
    [InlineData("iv-ab", "IV-AB")]
    public void NormalizeDocumentField_Normalizes(string? input, string expected)
        => PersonalDataNormalizer.NormalizeDocumentField(input).ShouldBe(expected);

    [Theory]
    [InlineData("500100732259", "500100732259")]
    [InlineData("112-233-445 95", "11223344595")]
    [InlineData("-", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void NormalizeDigitsOnly_StripsNonDigits_AbsentReturnsNull(string? input, string? expected)
        => PersonalDataNormalizer.NormalizeDigitsOnly(input).ShouldBe(expected);

    [Fact]
    public void FormatDate_UsesCanonicalForm()
        => PersonalDataNormalizer.FormatDate(new DateOnly(1990, 1, 5)).ShouldBe("1990-01-05");

    /// <summary>
    /// § 5.1: decomposed Unicode (NFC-несовместимый) нормализуется в composed форму.
    /// «Ё» как U+0415 + U+0308 должно совпадать с предкомпонованным U+0401.
    /// </summary>
    [Fact]
    public void DecomposedUnicode_IsNormalizedToComposedForm()
    {
        // "Ё" composed: U+0401
        var composed = "Ёлкин";
        // "E" + combining diaeresis = decomposed "Ё": U+0415 U+0308
        var decomposed = "\u0415\u0308лкин";

        PersonalDataNormalizer.NormalizeNameComponent(decomposed)
            .ShouldBe(PersonalDataNormalizer.NormalizeNameComponent(composed));
        PersonalDataNormalizer.NormalizeNameComponent(decomposed).ShouldBe("ЁЛКИН");
    }

    /// <summary>
    /// § 5.1: leading и inner whitespace схлопываются, trailing обрезается.
    /// </summary>
    [Theory]
    [InlineData("  Иванов   Пётр ", "ИВАНОВ ПЁТР")]
    [InlineData("\tИванов\nПётр", "ИВАНОВ ПЁТР")]
    [InlineData("Иванов", "ИВАНОВ")]
    public void Whitespace_LeadingCollapsed_TrailingTrimmed(string input, string expected)
        => PersonalDataNormalizer.NormalizeNameComponent(input).ShouldBe(expected);
}

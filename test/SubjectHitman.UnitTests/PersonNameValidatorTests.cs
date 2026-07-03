using FluentValidation;
using FluentValidation.TestHelper;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Endpoints;

namespace SubjectHitman.UnitTests;

public class PersonNameValidatorTests
{
    private readonly PersonNameValidator _validator = new();

    private static PersonNameData ValidName() => new("Иванов", "Иван", "Иванович");

    [Fact]
    public void ValidName_NoErrors()
        => _validator.TestValidate(ValidName()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingLastName_Fails(string? lastName)
        => _validator.TestValidate(ValidName() with { LastName = lastName! })
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingFirstName_Fails(string? firstName)
        => _validator.TestValidate(ValidName() with { FirstName = firstName! })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void MissingMiddleName_IsValid()
        => _validator.TestValidate(ValidName() with { MiddleName = null })
            .ShouldNotHaveAnyValidationErrors();
}

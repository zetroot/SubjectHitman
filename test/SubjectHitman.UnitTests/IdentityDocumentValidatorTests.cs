using FluentValidation;
using FluentValidation.TestHelper;
using SubjectHitman.Abstractions;
using SubjectHitman.Api.Endpoints;

namespace SubjectHitman.UnitTests;

public class IdentityDocumentValidatorTests
{
    private readonly IdentityDocumentValidator _validator = new();

    private static IdentityDocumentData ValidDocument() => new("21", "4510", "123456", new DateOnly(2010, 5, 20));

    [Fact]
    public void ValidDocument_NoErrors()
        => _validator.TestValidate(ValidDocument()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingTypeCode_Fails(string? typeCode)
        => _validator.TestValidate(ValidDocument() with { TypeCode = typeCode! })
            .ShouldHaveValidationErrorFor(x => x.TypeCode);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingNumber_Fails(string? number)
        => _validator.TestValidate(ValidDocument() with { Number = number! })
            .ShouldHaveValidationErrorFor(x => x.Number);

    [Fact]
    public void MissingIssueDate_IsValid()
        => _validator.TestValidate(ValidDocument() with { IssueDate = null })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptySeries_IsValid()
        => _validator.TestValidate(ValidDocument() with { Series = "" })
            .ShouldNotHaveAnyValidationErrors();
}

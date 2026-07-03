using FluentValidation;
using FluentValidation.TestHelper;
using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Api.Endpoints;

namespace SubjectHitman.UnitTests;

public class UsageQueryValidationTests
{
    private readonly UsageQueryRequestValidator _validator = new();

    private static UsageQueryRequest ValidRequest() => new(
        LastName: "Иванов",
        FirstName: "Иван",
        MiddleName: "Иванович",
        BirthDate: new DateOnly(1990, 1, 15),
        Document: new IdentityDocumentData("21", "4510", "123456", new DateOnly(2010, 5, 20)),
        PreviousName: null,
        PreviousDocument: null,
        Inn: "500100732259",
        Snils: "112-233-445 95");

    [Fact]
    public void ValidRequest_NoErrors()
        => _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void MissingLastName_Fails()
        => _validator.TestValidate(ValidRequest() with { LastName = "" })
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Fact]
    public void FutureBirthDate_Fails()
        => _validator.TestValidate(ValidRequest() with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) })
            .ShouldHaveValidationErrorFor(x => x.BirthDate);

    [Fact]
    public void MissingDocumentIssueDate_Fails()
        => _validator.TestValidate(ValidRequest() with
        {
            Document = new IdentityDocumentData("21", "4510", "123456", null),
        }).ShouldHaveValidationErrorFor(x => x.Document.IssueDate);

    [Fact]
    public void InvalidInnLength_Fails()
        => _validator.TestValidate(ValidRequest() with { Inn = "12345" })
            .ShouldHaveValidationErrorFor(x => x.Inn);

    [Fact]
    public void AbsentInn_IsValid()
        => _validator.TestValidate(ValidRequest() with { Inn = "-" })
            .ShouldNotHaveValidationErrorFor(x => x.Inn);

    [Fact]
    public void SnilsWithSeparators_IsValid()
        => _validator.TestValidate(ValidRequest() with { Snils = "112-233-445 95" })
            .ShouldNotHaveValidationErrorFor(x => x.Snils);

    [Fact]
    public void PreviousDocumentWithoutIssueDate_IsValid()
        => _validator.TestValidate(ValidRequest() with
        {
            PreviousDocument = new IdentityDocumentData("21", "4501", "654321", null),
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void PreviousDocumentWithoutNumber_Fails()
        => _validator.TestValidate(ValidRequest() with
        {
            PreviousDocument = new IdentityDocumentData("21", "4501", "", null),
        }).ShouldHaveValidationErrorFor(x => x.PreviousDocument!.Number);

    [Fact]
    public void PreviousNameWithoutLastName_Fails()
        => _validator.TestValidate(ValidRequest() with
        {
            PreviousName = new PersonNameData("", "Иван", null),
        }).ShouldHaveValidationErrorFor(x => x.PreviousName!.LastName);
}

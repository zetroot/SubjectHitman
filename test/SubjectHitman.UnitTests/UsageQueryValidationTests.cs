using SubjectHitman.Abstractions;
using SubjectHitman.Abstractions.Api;
using SubjectHitman.Api.Endpoints;

namespace SubjectHitman.UnitTests;

public class UsageQueryValidationTests
{
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
        => Assert.Empty(UsageQueryEndpoint.Validate(ValidRequest()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingLastName_Fails(string lastName)
        => Assert.Contains("lastName", UsageQueryEndpoint.Validate(ValidRequest() with { LastName = lastName }).Keys);

    [Fact]
    public void MissingFirstName_Fails()
        => Assert.Contains("firstName", UsageQueryEndpoint.Validate(ValidRequest() with { FirstName = "" }).Keys);

    [Fact]
    public void FutureBirthDate_Fails()
        => Assert.Contains(
            "birthDate",
            UsageQueryEndpoint.Validate(ValidRequest() with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) }).Keys);

    [Fact]
    public void MissingDocumentIssueDate_Fails()
        => Assert.Contains(
            "document.issueDate",
            UsageQueryEndpoint.Validate(ValidRequest() with
            {
                Document = new IdentityDocumentData("21", "4510", "123456", null),
            }).Keys);

    [Fact]
    public void MissingDocumentNumber_Fails()
        => Assert.Contains(
            "document.number",
            UsageQueryEndpoint.Validate(ValidRequest() with
            {
                Document = new IdentityDocumentData("21", "4510", "", new DateOnly(2010, 5, 20)),
            }).Keys);

    [Theory]
    [InlineData("12345")]           // too short
    [InlineData("1234567890123")]   // too long
    public void InvalidInnLength_Fails(string inn)
        => Assert.Contains("inn", UsageQueryEndpoint.Validate(ValidRequest() with { Inn = inn }).Keys);

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(null)]
    public void AbsentInn_IsValid(string? inn)
        => Assert.DoesNotContain("inn", UsageQueryEndpoint.Validate(ValidRequest() with { Inn = inn }).Keys);

    [Fact]
    public void InvalidSnilsLength_Fails()
        => Assert.Contains("snils", UsageQueryEndpoint.Validate(ValidRequest() with { Snils = "123" }).Keys);

    [Fact]
    public void SnilsWithSeparators_IsValid()
        => Assert.DoesNotContain("snils", UsageQueryEndpoint.Validate(ValidRequest() with { Snils = "112-233-445 95" }).Keys);

    [Fact]
    public void PreviousDocumentWithoutIssueDate_IsValid()
        => Assert.Empty(UsageQueryEndpoint.Validate(ValidRequest() with
        {
            PreviousDocument = new IdentityDocumentData("21", "4501", "654321", null),
        }));

    [Fact]
    public void PreviousDocumentWithoutNumber_Fails()
        => Assert.Contains(
            "previousDocument.number",
            UsageQueryEndpoint.Validate(ValidRequest() with
            {
                PreviousDocument = new IdentityDocumentData("21", "4501", "", null),
            }).Keys);

    [Fact]
    public void PreviousNameWithoutLastName_Fails()
        => Assert.Contains(
            "previousName.lastName",
            UsageQueryEndpoint.Validate(ValidRequest() with
            {
                PreviousName = new PersonNameData("", "Иван", null),
            }).Keys);
}

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The declared government-issued document of the Reference BQ (roadmap 1.20, ADR 0032): type and number required, name and
// validity period optional, every invalid part named by its BIAN path relative to the document.
public sealed class GovernmentIssuedDocumentTests
{
    private static readonly PartyDocumentInput _passport = new(
        " PersonalDocument ",
        " 23AB45678 ",
        " Passport ",
        "2023-04-01",
        "2033-03-31T00:00:00Z");

    [Fact]
    public void A_full_document_is_read_and_trimmed() =>
        GovernmentIssuedDocument.TryCreate(_passport).Value.ShouldBe(new GovernmentIssuedDocument(
            PartyDocumentType.PersonalDocument,
            GovernmentIssuedDocumentNumber.TryCreate("23AB45678").Value,
            GovernmentIssuedDocumentName.TryCreate("Passport").Value,
            new DateOnly(2023, 4, 1),
            new DateOnly(2033, 3, 31)));

    [Fact]
    public void Type_and_number_are_enough() =>
        GovernmentIssuedDocument.TryCreate(new PartyDocumentInput("workdocument", "WP-1")).Value
            .ShouldBe(new GovernmentIssuedDocument(
                PartyDocumentType.WorkDocument,
                GovernmentIssuedDocumentNumber.TryCreate("WP-1").Value,
                null,
                null,
                null));

    [Fact]
    public void A_missing_type_and_number_are_both_named() =>
        GovernmentIssuedDocument.TryCreate(new PartyDocumentInput(DocumentName: "Passport")).Error
            .FieldErrors.Keys.Order(StringComparer.Ordinal).ShouldBe(
            [
                "DocumentReference.DocumentIdentification.IdentifierValue",
                "PartyDocumentType",
            ]);

    [Fact]
    public void An_unknown_type_is_invalid() =>
        GovernmentIssuedDocument.TryCreate(_passport with { PartyDocumentType = "DrivingLicence" }).Error
            .FieldErrors.Keys.ShouldBe(["PartyDocumentType"]);

    [Theory]
    [InlineData(35, true)]
    [InlineData(36, false)]
    public void The_number_is_at_most_35_characters(int length, bool accepted) =>
        GovernmentIssuedDocument.TryCreate(_passport with { DocumentNumber = new string('9', length) }).IsSuccess.ShouldBe(accepted);

    [Theory]
    [InlineData(140, true)]
    [InlineData(141, false)]
    public void The_name_is_at_most_140_characters(int length, bool accepted) =>
        GovernmentIssuedDocument.TryCreate(_passport with { DocumentName = new string('n', length) }).IsSuccess.ShouldBe(accepted);

    [Fact]
    public void A_validity_that_ends_before_it_starts_is_invalid() =>
        GovernmentIssuedDocument.TryCreate(_passport with { ValidFrom = "2033-04-01", ValidTo = "2023-03-31" }).Error
            .FieldErrors.Keys.ShouldBe(["DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent"]);

    [Fact]
    public void A_validity_of_one_day_is_accepted() =>
        GovernmentIssuedDocument.TryCreate(_passport with { ValidFrom = "2024-01-01", ValidTo = "2024-01-01" }).IsSuccess.ShouldBeTrue();

    [Fact]
    public void A_date_that_is_not_iso_is_named_with_its_path() =>
        GovernmentIssuedDocument.TryCreate(_passport with { ValidFrom = "01/04/2023" }).Error
            .FieldErrors.Keys.ShouldBe(["DocumentReference.DocumentValidityPeriod.FromDateTime.DateTimeContent"]);

    [Fact]
    public void An_expired_document_can_be_recorded() =>
        GovernmentIssuedDocument.TryCreate(_passport with { ValidFrom = "2000-01-01", ValidTo = "2010-01-01" }).IsSuccess.ShouldBeTrue();

    [Fact]
    public void A_document_whose_parts_are_all_blank_is_blank() =>
        new PartyDocumentInput(" ", "", null, " ", null).IsBlank.ShouldBeTrue();
}

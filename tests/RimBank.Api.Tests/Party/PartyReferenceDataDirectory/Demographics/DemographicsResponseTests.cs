using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.9: the Demographics responses, from the acknowledgements and from the view, in BIAN terms (ADR 0024, ADR 0028).
public sealed class DemographicsResponseTests
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);
    private static readonly PartyReferenceDataDirectoryEntryId _entryId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7());
    private static readonly DemographicsId _demographicsId = DemographicsId.From(Guid.CreateVersion7());

    private static readonly DemographicsRecord _verified = new(
        new PartyDemographics("B2", new Employment("Contoso Ltd", "Accountant"), "Contoso Ltd since 2020", "MSc Finance", "Large print"),
        DemographicsVerificationStatus.Verified,
        _openDate.AddDays(1));

    private static readonly DemographicsResponse _expected = new(
        _entryId.Value,
        _demographicsId.Value,
        "B2",
        new DemographicsEmployment("Contoso Ltd", "Accountant"),
        "Contoso Ltd since 2020",
        "MSc Finance",
        "Large print",
        "Verified",
        _openDate.AddDays(1));

    [Fact]
    public void Both_acknowledgements_answer_the_decided_demographics()
    {
        DemographicsResponse.From(new UpdateDemographicsAcknowledged(_entryId, _demographicsId, 3, _verified)).ShouldBe(_expected);
        DemographicsResponse.From(new ExchangeDemographicsAcknowledged(_entryId, _demographicsId, 3, _verified)).ShouldBe(_expected);
    }

    [Fact]
    public void The_view_answers_its_demographics_section() =>
        DemographicsResponse.From(View(_verified)).ShouldBe(_expected);

    [Fact]
    public void Demographics_never_updated_answer_only_the_ids_and_unverified()
    {
        var json = JsonSerializer.Serialize(DemographicsResponse.From(View(null)), JsonSerializerOptions.Web);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["PartyReferenceDataDirectoryEntryId", "DemographicsId", "VerificationStatus"]);
        document.RootElement.GetProperty("VerificationStatus").GetString().ShouldBe("Unverified");
    }

    [Fact]
    public void The_response_uses_BIAN_property_names_and_never_has_EthnicityReligion()
    {
        var json = JsonSerializer.Serialize(_expected, JsonSerializerOptions.Web);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(
        [
            "PartyReferenceDataDirectoryEntryId",
            "DemographicsId",
            "Socio-EconomicClassification",
            "Employment",
            "EmploymentHistory",
            "EducationHistory",
            "ServicingConstraints",
            "VerificationStatus",
            "VerificationDate",
        ]);
        document.RootElement.GetProperty("Employment").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["PartyReference", "InvolvementReference"]);
    }

    private static PartyReferenceDataDirectoryEntryView View(DemographicsRecord? demographics) =>
        new(
            _entryId.Value,
            Version: 3,
            PartyType.Person,
            _openDate,
            _demographicsId,
            PartyReferenceId.From(Guid.CreateVersion7()),
            BankRelationsId.From(Guid.CreateVersion7()),
            Demographics: demographics);
}

using System.Text.Json;
using System.Text.Json.Nodes;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The corporate and officer references of the Reference BQ (roadmap 1.21, ADR 0034): the officer's role, the partial
// update of the references, and a Reference stored before 1.21 reading back unchanged.
public sealed class PartyCorporateReferencesTests
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    [Fact]
    public void The_role_is_trimmed() =>
        CompanyOfficerRole.TryCreate("  Director ").Value.Value.ShouldBe("Director");

    [Fact]
    public void Thirty_five_characters_of_role_are_accepted() =>
        CompanyOfficerRole.TryCreate(new string('r', 35)).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(36)]
    [InlineData(0)]
    public void A_role_too_long_or_blank_is_invalid(int length) =>
        CompanyOfficerRole.TryCreate(new string('r', length)).Error.Code.ShouldBe("party.reference-invalid");

    [Fact]
    public void The_role_is_a_plain_json_string() =>
        JsonSerializer.Serialize(CompanyOfficerRole.TryCreate("Director").Value).ShouldBe("\"Director\"");

    [Fact]
    public void Empty_references_have_nothing()
    {
        PartyCorporateReferences.Empty.HasAny.ShouldBeFalse();
        (PartyCorporateReferences.Empty with { LegalEntity = CompanyId }).HasAny.ShouldBeTrue();
    }

    [Fact]
    public void The_changes_set_keep_and_clear_each_reference() =>
        CorporateReferences.Apply(PartyReferenceChanges.None with
        {
            CorporateCustomer = new(null),
            CompanyOfficerRole = new(CompanyOfficerRole.TryCreate("Secretary").Value),
        })
            .ShouldBe(CorporateReferences with
            {
                CorporateCustomer = null,
                CompanyOfficerRole = CompanyOfficerRole.TryCreate("Secretary").Value,
            });

    [Fact]
    public void The_reference_records_them_only_when_one_is_set()
    {
        var recorded = PartyReferenceDetails.Empty.Apply(CorporateReferenceChangesTo(CorporateReferences));
        recorded.CorporateReferences.ShouldBe(CorporateReferences);

        var cleared = recorded.Apply(CorporateReferenceChangesTo(PartyCorporateReferences.Empty));
        cleared.CorporateReferences.ShouldBeNull();
        cleared.CurrentCorporateReferences.ShouldBe(PartyCorporateReferences.Empty);
    }

    [Fact]
    public void Another_update_leaves_the_references_unchanged()
    {
        var details = FullestReferenceDetails with { CorporateReferences = CorporateReferences };

        details.Apply(PartyReferenceChanges.None with { PartyAddress = new(null) })
            .ShouldBe(details with { Addresses = Addresses with { PartyAddress = null } });
    }

    [Fact]
    public void A_reference_with_references_survives_a_json_round_trip()
    {
        var details = FullestReferenceDetails with { CorporateReferences = CorporateReferences };

        JsonSerializer.Deserialize<PartyReferenceDetails>(JsonSerializer.Serialize(details, _json), _json).ShouldBe(details);
    }

    [Fact]
    public void A_reference_stored_before_1_21_reads_back_unchanged()
    {
        var stored = JsonNode.Parse(JsonSerializer.Serialize(FullestReferenceDetails, _json))!.AsObject();
        stored.Remove(nameof(PartyReferenceDetails.CorporateReferences)).ShouldBeTrue();

        var read = JsonSerializer.Deserialize<PartyReferenceDetails>(stored.ToJsonString(), _json).ShouldNotBeNull();

        read.ShouldBe(FullestReferenceDetails);
        read.CurrentCorporateReferences.ShouldBe(PartyCorporateReferences.Empty);
    }
}

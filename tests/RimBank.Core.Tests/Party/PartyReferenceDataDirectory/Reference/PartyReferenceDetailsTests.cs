using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Applying a partial Reference Update to the names, date of birth and residency (roadmap 1.18).
public sealed class PartyReferenceDetailsTests
{
    [Fact]
    public void No_change_keeps_every_value() =>
        FullReferenceDetails.Apply(PartyReferenceChanges.None).ShouldBe(FullReferenceDetails);

    [Fact]
    public void Every_value_is_set_from_empty() =>
        PartyReferenceDetails.Empty.Apply(ReferenceChangesTo(FullReferenceDetails)).ShouldBe(FullReferenceDetails);

    [Fact]
    public void Each_value_changes_on_its_own()
    {
        var updated = FullReferenceDetails.Apply(PartyReferenceChanges.None with
        {
            PartyNameSalutation = new PartyReferenceChange<PartyNameSalutation?>(null),
            DateOfBirth = new PartyReferenceChange<DateOnly?>(new DateOnly(1991, 1, 2)),
            PartyCountryOfResidence = new PartyReferenceChange<CountryCode?>(CountryCode.From("LU")),
        });

        updated.ShouldBe(FullReferenceDetails with
        {
            Names = Names with { Salutation = null },
            DateOfBirth = new DateOnly(1991, 1, 2),
            Residency = Residency with { CountryOfResidence = CountryCode.From("LU") },
        });
    }

    [Fact]
    public void A_given_residency_status_replaces_the_whole_status() =>
        FullReferenceDetails.Apply(PartyReferenceChanges.None with
        {
            ResidencyStatus = new PartyReferenceChange<ResidencyStatus?>(new ResidencyStatus(ResidencyStatusCode.Resident, null)),
        })
            .CurrentResidency.ResidencyStatus.ShouldBe(new ResidencyStatus(ResidencyStatusCode.Resident, null));

    [Fact]
    public void Clearing_every_name_and_residency_part_leaves_them_unrecorded()
    {
        var cleared = FullReferenceDetails.Apply(PartyReferenceChanges.None with
        {
            PartyFullLegalName = new PartyReferenceChange<PartyFullLegalName?>(null),
            PartyNameSalutation = new PartyReferenceChange<PartyNameSalutation?>(null),
            DateOfBirth = new PartyReferenceChange<DateOnly?>(null),
            Nationality = new PartyReferenceChange<CountryCode?>(null),
            PartyCountryOfResidence = new PartyReferenceChange<CountryCode?>(null),
            ResidencyStatus = new PartyReferenceChange<ResidencyStatus?>(null),
        });

        cleared.ShouldBe(new PartyReferenceDetails(Identifiers));
        cleared.Names.ShouldBeNull();
        cleared.Residency.ShouldBeNull();
        cleared.CurrentNames.ShouldBe(PartyNames.Empty);
        cleared.CurrentResidency.ShouldBe(PartyResidency.Empty);
    }

    [Fact]
    public void An_identifier_update_leaves_the_names_and_residency_unchanged() =>
        FullReferenceDetails.Apply(ReferenceChangesTo(PartyIdentifiers.Empty))
            .ShouldBe(FullReferenceDetails with { Identifiers = PartyIdentifiers.Empty });

    [Fact]
    public void Empty_names_and_residency_have_nothing()
    {
        PartyNames.Empty.HasAny.ShouldBeFalse();
        Names.HasAny.ShouldBeTrue();
        PartyResidency.Empty.HasAny.ShouldBeFalse();
        (PartyResidency.Empty with { Nationality = CountryCode.From("FR") }).HasAny.ShouldBeTrue();
    }
}

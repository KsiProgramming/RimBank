using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Applying a partial Reference Update to the party identifiers (roadmap 1.10).
public sealed class PartyIdentifiersTests
{
    [Fact]
    public void No_change_keeps_every_identifier() =>
        Identifiers.Apply(PartyReferenceChanges.None).ShouldBe(Identifiers);

    [Fact]
    public void Each_identifier_changes_on_its_own()
    {
        var otherLei = LegalEntityIdentifier.TryCreate("529900T8BM49AURSDO55").Value;

        var updated = Identifiers.Apply(PartyReferenceChanges.None with
        {
            TaxIdentificationNumber = new PartyIdentifierChange<TaxIdentificationNumber>(null),
            LegalEntityIdentifier = new PartyIdentifierChange<LegalEntityIdentifier>(otherLei),
        });

        updated.ShouldBe(Identifiers with { TaxIdentificationNumber = null, LegalEntityIdentifier = otherLei });
    }

    [Fact]
    public void Empty_identifiers_have_none()
    {
        PartyIdentifiers.Empty.HasAny.ShouldBeFalse();
        Identifiers.HasAny.ShouldBeTrue();
        (PartyIdentifiers.Empty with { NationalRegistrationNumber = Identifiers.NationalRegistrationNumber }).HasAny.ShouldBeTrue();
    }

    [Fact]
    public void A_reference_record_applies_the_changes_to_its_identifiers() =>
        PartyReferenceDetails.Empty.Apply(ReferenceChangesTo(Identifiers)).ShouldBe(ReferenceDetails);
}

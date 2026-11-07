using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Update decision (BIAN PartyReferenceDataDirectoryEntry/Update, roadmap 1.8). The expected version is the
// handler's check, not the decision's.
public sealed class UpdateTests
{
    [Theory]
    [InlineData(PartyType.Person, PartyType.Organisation)]
    [InlineData(PartyType.Organisation, PartyType.Person)]
    public void Updating_the_party_type_records_the_new_type_and_the_refresh(PartyType registered, PartyType updated) =>
        EntrySpecification.Given(Registered(registered))
            .When(Update(updated))
            .Then(Updated(updated));

    [Fact]
    public void An_update_without_a_party_type_keeps_the_current_one_and_records_the_refresh() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(Update(partyType: null))
            .Then(Updated(PartyType.Organisation));

    [Fact]
    public void An_update_to_the_same_party_type_is_still_a_refresh() =>
        EntrySpecification.Given(Registered(PartyType.Person))
            .When(Update(PartyType.Person))
            .Then(Updated(PartyType.Person));

    [Fact]
    public void A_later_update_starts_from_the_type_the_previous_one_left() =>
        EntrySpecification.Given(Registered(PartyType.Person), Updated(PartyType.Organisation))
            .When(Update(partyType: null))
            .Then(Updated(PartyType.Organisation));

    [Fact]
    public void An_entry_that_is_not_registered_cannot_be_updated() =>
        EntrySpecification.Given()
            .When(Update())
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void A_party_type_that_is_neither_person_nor_organisation_is_refused()
    {
        var error = EntrySpecification.Given(Registered())
            .When(Update((PartyType)99))
            .ThenFails(ErrorKind.Validation, "party.party-type-invalid");

        error.FieldErrors.Keys.ShouldBe(["PartyType"]);
    }
}

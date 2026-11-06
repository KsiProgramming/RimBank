using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests;

// The Register decision (BIAN PartyReferenceDataDirectoryEntry/Register, ADR 0024).
public sealed class RegisterTests
{
    [Theory]
    [InlineData(PartyType.Person)]
    [InlineData(PartyType.Organisation)]
    public void Registering_a_new_entry_records_PartyRegistered_with_its_singleton_BQ_ids(PartyType partyType) =>
        EntrySpecification.Given()
            .When(Register(partyType))
            .Then(Registered(partyType));

    [Fact]
    public void An_entry_registers_only_once() =>
        EntrySpecification.Given(Registered())
            .When(Register(PartyType.Organisation))
            .ThenFails(ErrorKind.Conflict, "party.party-already-registered");

    [Fact]
    public void A_party_type_that_is_neither_person_nor_organisation_is_refused()
    {
        var error = EntrySpecification.Given()
            .When(Register((PartyType)99))
            .ThenFails(ErrorKind.Validation, "party.party-type-invalid");

        error.FieldErrors.Keys.ShouldBe(["PartyType"]);
    }
}

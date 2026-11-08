using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Reference Update decision (BIAN Reference/Update, roadmap 1.10). The expected version is the handler's check, not
// the decision's.
public sealed class UpdatePartyReferenceTests
{
    [Fact]
    public void The_first_update_records_the_full_reference() =>
        EntrySpecification.Given(Registered())
            .When(UpdateReference())
            .Then(ReferenceUpdated());

    [Fact]
    public void A_partial_update_records_the_full_resulting_reference() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated())
            .When(UpdateReference(PartyReferenceChanges.None with
            {
                NationalRegistrationNumber = new PartyIdentifierChange<NationalRegistrationNumber>(null),
            }))
            .Then(ReferenceUpdated(new PartyReferenceDetails(Identifiers with { NationalRegistrationNumber = null })));

    [Fact]
    public void An_update_that_changes_nothing_is_still_recorded() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated())
            .When(UpdateReference(PartyReferenceChanges.None))
            .Then(ReferenceUpdated());

    [Fact]
    public void A_reference_update_keeps_the_demographics() =>
        EntrySpecification.Given(Registered(), DetailsUpdated(), DetailsExchanged())
            .When(UpdateReference())
            .Then(ReferenceUpdated());

    [Fact]
    public void An_entry_that_is_not_registered_has_no_reference_to_update() =>
        EntrySpecification.Given()
            .When(UpdateReference())
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void Another_reference_id_than_the_entry_s_is_not_found() =>
        EntrySpecification.Given(Registered())
            .When(UpdateReference() with { PartyReferenceId = PartyReferenceId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5))) })
            .ThenFails(ErrorKind.NotFound, "party.behavior-qualifier-not-found");
}

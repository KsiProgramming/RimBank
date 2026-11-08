using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The BankRelations Retrieve flow on the query side (BIAN BankRelations/Retrieve, roadmap 1.12): the handler only reads
// the entry's view and checks that the BankRelations id is the entry's.
public sealed class RetrieveBankRelationsHandlerTests
{
    private readonly InMemoryPartyDirectoryEnquiry _enquiry = new();

    [Fact]
    public async Task The_entry_s_bank_relations_are_retrieved_from_its_view()
    {
        _enquiry.Seed(EntryId, Registered(), ReferenceUpdated(), RelationsUpdated(ManagedRelation));

        var view = (await Retrieve(BankRelations)).Value;

        view.Version.ShouldBe(3);
        view.CurrentBankRelations.ShouldBe(ManagedRelation);
    }

    [Fact]
    public async Task Bank_relations_never_updated_are_empty()
    {
        _enquiry.Seed(EntryId, Registered());

        var view = (await Retrieve(BankRelations)).Value;

        view.CurrentBankRelations.ShouldBe(BankRelationsDetails.Empty);
    }

    [Fact]
    public async Task An_unknown_entry_is_party_not_found() =>
        (await Retrieve(BankRelations)).Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);

    [Fact]
    public async Task A_foreign_bank_relations_id_is_behavior_qualifier_not_found()
    {
        _enquiry.Seed(EntryId, Registered());

        (await Retrieve(BankRelationsId.From(Guid.CreateVersion7(OpenDate.AddDays(1))))).Error
            .ShouldBe(PartyReferenceDataDirectoryEntryErrors.BehaviorQualifierNotFound);
    }

    private Task<Result<PartyReferenceDataDirectoryEntryView>> Retrieve(BankRelationsId bankRelationsId) =>
        RetrieveBankRelationsHandler.HandleAsync(new RetrieveBankRelations(EntryId, bankRelationsId), _enquiry, CancellationToken.None);
}

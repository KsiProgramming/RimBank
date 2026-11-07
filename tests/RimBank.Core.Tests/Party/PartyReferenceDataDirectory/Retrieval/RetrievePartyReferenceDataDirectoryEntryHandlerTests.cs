using RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry;
using RimBank.Party.PartyReferenceDataDirectory.Tests;
using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Retrieval.Tests;

// The Retrieve flow on the query side (BIAN PartyReferenceDataDirectoryEntry/Retrieve, ADR 0024): the handler only
// reads the entry's view, and an unknown entry is party.party-not-found.
public sealed class RetrievePartyReferenceDataDirectoryEntryHandlerTests
{
    private readonly InMemoryPartyDirectoryEnquiry _enquiry = new();

    [Fact]
    public async Task A_registered_entry_is_retrieved_as_its_view()
    {
        _enquiry.Seed(EntryId, Registered(PartyType.Organisation));

        var result = await Retrieve(EntryId);

        result.Value.ShouldBe(new PartyReferenceDataDirectoryEntryView(
            EntryId.Value,
            Version: 1,
            PartyType.Organisation,
            OpenDate,
            DemographicsId,
            PartyReferenceId,
            BankRelationsId));
    }

    [Fact]
    public async Task An_unknown_entry_is_party_not_found()
    {
        var result = await Retrieve(EntryId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
        result.Error.Code.ShouldBe("party.party-not-found");
    }

    [Fact]
    public async Task Each_entry_is_retrieved_as_its_own_view()
    {
        var otherId = PartyReferenceDataDirectoryEntryId.From(Guid.CreateVersion7(OpenDate.AddMinutes(1)));
        _enquiry.Seed(EntryId, Registered(PartyType.Person));
        _enquiry.Seed(otherId, Registered(PartyType.Organisation) with { EntryId = otherId });

        var first = (await Retrieve(EntryId)).Value;
        var second = (await Retrieve(otherId)).Value;

        first.EntryId.ShouldBe(EntryId);
        first.PartyType.ShouldBe(PartyType.Person);
        second.EntryId.ShouldBe(otherId);
        second.PartyType.ShouldBe(PartyType.Organisation);
    }

    private Task<Result<PartyReferenceDataDirectoryEntryView>> Retrieve(PartyReferenceDataDirectoryEntryId entryId) =>
        RetrievePartyReferenceDataDirectoryEntryHandler.HandleAsync(
            new RetrievePartyReferenceDataDirectoryEntry(entryId),
            _enquiry,
            TestContext.Current.CancellationToken);
}

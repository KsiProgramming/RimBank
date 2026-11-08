using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Reference Retrieve flow on the query side (BIAN Reference/Retrieve, roadmap 1.10): the handler only reads the
// entry's view and checks that the Reference id is the entry's.
public sealed class RetrievePartyReferenceHandlerTests
{
    private readonly InMemoryPartyDirectoryEnquiry _enquiry = new();

    [Fact]
    public async Task The_entry_s_reference_is_retrieved_from_its_view()
    {
        _enquiry.Seed(EntryId, Registered(), DetailsUpdated(), ReferenceUpdated());

        var view = (await Retrieve(Reference)).Value;

        view.Version.ShouldBe(3);
        view.CurrentReference.ShouldBe(ReferenceDetails);
    }

    [Fact]
    public async Task A_reference_never_updated_is_empty()
    {
        _enquiry.Seed(EntryId, Registered());

        var view = (await Retrieve(Reference)).Value;

        view.CurrentReference.ShouldBe(PartyReferenceDetails.Empty);
    }

    [Fact]
    public async Task An_unknown_entry_is_party_not_found() =>
        (await Retrieve(Reference)).Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);

    [Fact]
    public async Task A_foreign_reference_id_is_behavior_qualifier_not_found()
    {
        _enquiry.Seed(EntryId, Registered());

        var result = await Retrieve(PartyReferenceId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5))));

        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
        result.Error.Code.ShouldBe("party.behavior-qualifier-not-found");
    }

    private Task<Result<PartyReferenceDataDirectoryEntryView>> Retrieve(PartyReferenceId referenceId) =>
        RetrievePartyReferenceHandler.HandleAsync(
            new RetrievePartyReference(EntryId, referenceId),
            _enquiry,
            TestContext.Current.CancellationToken);
}

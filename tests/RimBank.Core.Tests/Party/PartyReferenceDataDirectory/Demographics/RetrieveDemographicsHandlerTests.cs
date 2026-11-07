using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Demographics Retrieve flow on the query side (BIAN Demographics/Retrieve, roadmap 1.9): the handler only reads the
// entry's view and checks that the Demographics id is the entry's.
public sealed class RetrieveDemographicsHandlerTests
{
    private readonly InMemoryPartyDirectoryEnquiry _enquiry = new();

    [Fact]
    public async Task The_entry_s_demographics_are_retrieved_from_its_view()
    {
        _enquiry.Seed(EntryId, Registered(), DetailsUpdated(), DetailsExchanged());

        var view = (await Retrieve(Demographics)).Value;

        view.Version.ShouldBe(3);
        view.CurrentDemographics.ShouldBe(new DemographicsRecord(Details, DemographicsVerificationStatus.Verified, VerificationDate));
    }

    [Fact]
    public async Task Demographics_never_updated_are_empty_and_unverified()
    {
        _enquiry.Seed(EntryId, Registered());

        var view = (await Retrieve(Demographics)).Value;

        view.CurrentDemographics.ShouldBe(DemographicsRecord.Empty);
    }

    [Fact]
    public async Task An_unknown_entry_is_party_not_found()
    {
        var result = await Retrieve(Demographics);

        result.Error.ShouldBe(PartyReferenceDataDirectoryEntryErrors.PartyNotFound);
    }

    [Fact]
    public async Task A_foreign_demographics_id_is_behavior_qualifier_not_found()
    {
        _enquiry.Seed(EntryId, Registered());

        var result = await Retrieve(DemographicsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5))));

        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
        result.Error.Code.ShouldBe("party.behavior-qualifier-not-found");
    }

    private Task<Result<PartyReferenceDataDirectoryEntryView>> Retrieve(DemographicsId demographicsId) =>
        RetrieveDemographicsHandler.HandleAsync(
            new RetrieveDemographics(EntryId, demographicsId),
            _enquiry,
            TestContext.Current.CancellationToken);
}

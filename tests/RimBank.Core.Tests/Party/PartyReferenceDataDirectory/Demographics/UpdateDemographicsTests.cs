using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Demographics Update decision (BIAN Demographics/Update, roadmap 1.9, ADR 0028). The expected version is the
// handler's check, not the decision's.
public sealed class UpdateDemographicsTests
{
    [Fact]
    public void The_first_update_records_the_full_details_unverified() =>
        EntrySpecification.Given(Registered())
            .When(UpdateDetails())
            .Then(DetailsUpdated(Details, DemographicsVerificationStatus.Unverified, null));

    [Fact]
    public void A_partial_update_records_the_full_resulting_details() =>
        EntrySpecification.Given(Registered(), DetailsUpdated())
            .When(UpdateDetails(DemographicsChanges.None with { EducationHistory = "PhD" }))
            .Then(DetailsUpdated(Details with { EducationHistory = "PhD" }));

    [Fact]
    public void Changing_verified_details_makes_them_unverified() =>
        EntrySpecification.Given(Registered(), DetailsUpdated(), DetailsExchanged(DemographicsVerificationStatus.Verified))
            .When(UpdateDetails(DemographicsChanges.None with { SocioEconomicClassification = "C1" }))
            .Then(DetailsUpdated(Details with { SocioEconomicClassification = "C1" }, DemographicsVerificationStatus.Unverified, null));

    [Fact]
    public void An_update_that_changes_nothing_is_still_recorded_and_keeps_the_verification() =>
        EntrySpecification.Given(Registered(), DetailsUpdated(), DetailsExchanged(DemographicsVerificationStatus.Verified))
            .When(UpdateDetails(DemographicsChanges.None))
            .Then(DetailsUpdated(Details, DemographicsVerificationStatus.Verified, VerificationDate));

    [Fact]
    public void An_entry_that_is_not_registered_has_no_demographics_to_update() =>
        EntrySpecification.Given()
            .When(UpdateDetails())
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void Another_demographics_id_than_the_entry_s_is_not_found() =>
        EntrySpecification.Given(Registered())
            .When(UpdateDetails() with { DemographicsId = DemographicsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5))) })
            .ThenFails(ErrorKind.NotFound, "party.behavior-qualifier-not-found");
}

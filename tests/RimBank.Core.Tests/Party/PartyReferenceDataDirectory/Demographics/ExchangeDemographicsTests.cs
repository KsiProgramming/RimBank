using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The Demographics Exchange decision: the bank verifies or rejects the current details (BIAN Demographics/Exchange,
// roadmap 1.9, ADR 0028).
public sealed class ExchangeDemographicsTests
{
    [Theory]
    [InlineData(ExchangeActionType.Verify, DemographicsVerificationStatus.Verified)]
    [InlineData(ExchangeActionType.Reject, DemographicsVerificationStatus.Rejected)]
    public void Recorded_details_are_verified_or_rejected_at_the_verification_date(
        ExchangeActionType action,
        DemographicsVerificationStatus outcome) =>
        EntrySpecification.Given(Registered(), DetailsUpdated())
            .When(ExchangeDetails(action))
            .Then(DetailsExchanged(outcome));

    [Fact]
    public void Rejected_details_can_be_verified_later() =>
        EntrySpecification.Given(Registered(), DetailsUpdated(), DetailsExchanged(DemographicsVerificationStatus.Rejected))
            .When(ExchangeDetails(ExchangeActionType.Verify))
            .Then(DetailsExchanged(DemographicsVerificationStatus.Verified));

    [Fact]
    public void Details_that_were_never_recorded_cannot_be_exchanged() =>
        EntrySpecification.Given(Registered())
            .When(ExchangeDetails())
            .ThenFails(ErrorKind.BusinessRule, "party.demographics-not-recorded");

    [Fact]
    public void Details_that_were_all_cleared_cannot_be_exchanged() =>
        EntrySpecification.Given(Registered(), DetailsUpdated(PartyDemographics.Empty))
            .When(ExchangeDetails())
            .ThenFails(ErrorKind.BusinessRule, "party.demographics-not-recorded");

    [Fact]
    public void An_entry_that_is_not_registered_has_no_demographics_to_exchange() =>
        EntrySpecification.Given()
            .When(ExchangeDetails())
            .ThenFails(ErrorKind.NotFound, "party.party-not-found");

    [Fact]
    public void Another_demographics_id_than_the_entry_s_is_not_found() =>
        EntrySpecification.Given(Registered(), DetailsUpdated())
            .When(ExchangeDetails() with { DemographicsId = DemographicsId.From(Guid.CreateVersion7(OpenDate.AddMinutes(5))) })
            .ThenFails(ErrorKind.NotFound, "party.behavior-qualifier-not-found");

    [Fact]
    public void An_undefined_action_is_invalid() =>
        EntrySpecification.Given(Registered(), DetailsUpdated())
            .When(ExchangeDetails((ExchangeActionType)99))
            .ThenFails(ErrorKind.Validation, "party.exchange-action-type-invalid");
}

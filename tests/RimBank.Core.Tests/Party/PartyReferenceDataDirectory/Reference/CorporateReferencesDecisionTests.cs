using RimBank.SharedKernel;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;
using EntrySpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The corporate and officer references of the Reference Update decision (roadmap 1.21, ADR 0034): only an organisation's
// entry holds them, each reference the Update sets names another registered entry of the right party type (the facts the
// handler loaded from the write side), and a role needs an officer. The entry Update cannot make such an entry a person.
public sealed class CorporateReferencesDecisionTests
{
    private static readonly PartyReferenceDetails _withReferences = new(PartyIdentifiers.Empty, CorporateReferences: CorporateReferences);

    [Fact]
    public void An_organisation_records_its_corporate_customer_legal_entity_officer_and_role() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(CorporateReferenceChangesTo(CorporateReferences), ReferencedParties))
            .Then(ReferenceUpdated(_withReferences));

    [Fact]
    public void A_partial_update_keeps_the_references_it_does_not_send_without_checking_them_again() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficerRole = new(CompanyOfficerRole.TryCreate("Secretary").Value) }))
            .Then(ReferenceUpdated(_withReferences with
            {
                CorporateReferences = CorporateReferences with { CompanyOfficerRole = CompanyOfficerRole.TryCreate("Secretary").Value },
            }));

    [Fact]
    public void Clearing_every_reference_leaves_no_corporate_references() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(UpdateReference(CorporateReferenceChangesTo(PartyCorporateReferences.Empty)))
            .Then(ReferenceUpdated(PartyReferenceDetails.Empty));

    [Fact]
    public void A_person_s_entry_cannot_hold_corporate_references() =>
        EntrySpecification.Given(Registered(PartyType.Person))
            .When(UpdateReference(CorporateReferenceChangesTo(CorporateReferences), ReferencedParties))
            .ThenFails(ErrorKind.BusinessRule, "party.corporate-references-need-organisation");

    [Fact]
    public void A_person_may_clear_references_it_does_not_hold() =>
        EntrySpecification.Given(Registered(PartyType.Person))
            .When(UpdateReference(CorporateReferenceChangesTo(PartyCorporateReferences.Empty)))
            .Then(ReferenceUpdated(PartyReferenceDetails.Empty));

    [Fact]
    public void A_reference_to_the_entry_itself_is_refused_naming_its_path()
    {
        var error = EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { LegalEntity = new(EntryId) }, ReferencedParties))
            .ThenFails(ErrorKind.BusinessRule, "party.self-reference");

        error.Message.ShouldStartWith("CorporateCustomerLegalEntityReference.PartyReference");
    }

    [Fact]
    public void A_reference_to_an_entry_that_is_not_registered_is_refused_naming_its_path()
    {
        var error = EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficer = new(OfficerId) }, referencedParties: null))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-not-registered");

        error.Message.ShouldStartWith("CompanyOfficerReference.PartyReference");
    }

    [Fact]
    public void A_person_as_the_corporate_customer_is_a_type_mismatch() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { CorporateCustomer = new(OfficerId) }, ReferencedParties))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch")
            .Message.ShouldBe("CorporateCustomerReference.PartyReference must name a party of type Organisation.");

    [Fact]
    public void A_person_as_the_legal_entity_is_a_type_mismatch() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { LegalEntity = new(OfficerId) }, ReferencedParties))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch");

    [Fact]
    public void An_organisation_as_the_officer_is_a_type_mismatch() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficer = new(CompanyId) }, ReferencedParties))
            .ThenFails(ErrorKind.BusinessRule, "party.referenced-party-type-mismatch")
            .Message.ShouldBe("CompanyOfficerReference.PartyReference must name a party of type Person.");

    [Fact]
    public void A_role_without_an_officer_is_invalid() =>
        EntrySpecification.Given(Registered(PartyType.Organisation))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficerRole = new(CorporateReferences.CompanyOfficerRole) }))
            .ThenFails(ErrorKind.Validation, "party.reference-invalid")
            .FieldErrors.Keys.ShouldBe(["CompanyOfficerRole"]);

    [Fact]
    public void Clearing_the_officer_while_the_role_is_kept_is_invalid() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficer = new(null) }))
            .ThenFails(ErrorKind.Validation, "party.reference-invalid");

    [Fact]
    public void Clearing_the_officer_and_the_role_together_is_accepted() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(UpdateReference(PartyReferenceChanges.None with { CompanyOfficer = new(null), CompanyOfficerRole = new(null) }))
            .Then(ReferenceUpdated(_withReferences with
            {
                CorporateReferences = CorporateReferences with { CompanyOfficer = null, CompanyOfficerRole = null },
            }));

    [Fact]
    public void An_organisation_with_corporate_references_cannot_become_a_person() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(Update(PartyType.Person))
            .ThenFails(ErrorKind.BusinessRule, "party.corporate-references-need-organisation");

    [Fact]
    public void An_organisation_with_corporate_references_can_be_refreshed_as_an_organisation() =>
        EntrySpecification.Given(Registered(PartyType.Organisation), ReferenceUpdated(_withReferences))
            .When(Update(partyType: null))
            .Then(Updated(PartyType.Organisation));

    [Fact]
    public void An_organisation_whose_references_are_cleared_can_become_a_person() =>
        EntrySpecification.Given(
                Registered(PartyType.Organisation),
                ReferenceUpdated(_withReferences),
                ReferenceUpdated(PartyReferenceDetails.Empty))
            .When(Update(PartyType.Person))
            .Then(Updated(PartyType.Person));
}

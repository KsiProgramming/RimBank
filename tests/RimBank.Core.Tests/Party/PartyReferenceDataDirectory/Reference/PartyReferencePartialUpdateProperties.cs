using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The partial-update law of the Reference (roadmap 1.20): over every recorded field (1.10 to 1.20), an omitted change keeps
// the current value, a given one sets or clears it, and applying the same changes twice is the same as once.
public sealed class PartyReferencePartialUpdateProperties
{
    // One accessor per recorded field: its current value, and the change a request makes to it (Sent is false when omitted).
    private static readonly (string Name, Func<PartyReferenceDetails, object?> Value, Func<PartyReferenceChanges, (bool Sent, object? Value)> Change)[] _fields =
    [
        ("TaxIdentificationNumber", d => d.Identifiers.TaxIdentificationNumber, c => (c.TaxIdentificationNumber is not null, c.TaxIdentificationNumber?.Value)),
        ("NationalRegistrationNumber", d => d.Identifiers.NationalRegistrationNumber, c => (c.NationalRegistrationNumber is not null, c.NationalRegistrationNumber?.Value)),
        ("LegalEntityIdentifier", d => d.Identifiers.LegalEntityIdentifier, c => (c.LegalEntityIdentifier is not null, c.LegalEntityIdentifier?.Value)),
        ("PartyFullLegalName", d => d.CurrentNames.FullLegalName, c => Of(c.PartyFullLegalName)),
        ("PartyNameSalutation", d => d.CurrentNames.Salutation, c => Of(c.PartyNameSalutation)),
        ("DateofBirth", d => d.DateOfBirth, c => Of(c.DateOfBirth)),
        ("Nationality", d => d.CurrentResidency.Nationality, c => Of(c.Nationality)),
        ("PartyCountryOfResidence", d => d.CurrentResidency.CountryOfResidence, c => Of(c.PartyCountryOfResidence)),
        ("ResidencyStatus", d => d.CurrentResidency.ResidencyStatus, c => Of(c.ResidencyStatus)),
        ("ResidentialAddress", d => d.CurrentAddresses.ResidentialAddress, c => Of(c.ResidentialAddress)),
        ("PartyAddress", d => d.CurrentAddresses.PartyAddress, c => Of(c.PartyAddress)),
        ("CorporateAddress", d => d.CurrentAddresses.CorporateAddress, c => Of(c.CorporateAddress)),
        ("eMailAddress", d => d.CurrentContactPoints.EmailAddress, c => Of(c.EmailAddress)),
        ("CellPhoneNumber", d => d.CurrentContactPoints.MobileNumber, c => Of(c.MobileNumber)),
        ("SocialNetworkContacts", d => d.CurrentContactPoints.SocialNetwork, c => Of(c.SocialNetworkAddress)),
        ("PartyLegalStructure", d => d.CurrentLegalStanding.LegalStructure, c => Of(c.PartyLegalStructure)),
        ("PoliticalExposureType", d => d.CurrentLegalStanding.PoliticalExposureType, c => Of(c.PoliticalExposureType)),
        ("PoliticalExposureDescriptionRecord", d => d.CurrentLegalStanding.PoliticalExposureDescription, c => Of(c.PoliticalExposureDescription)),
        ("CustomerSinceDate", d => d.CurrentLegalStanding.CustomerSince, c => Of(c.CustomerSinceDate)),
        ("GovernmentIssuedDocumentDetails", d => d.CurrentDocuments.GovernmentIssuedDocument, c => Of(c.GovernmentIssuedDocument)),
        ("DocumentDirectoryEntryInstanceReference", d => d.CurrentDocuments.DocumentDirectoryEntry, c => Of(c.DocumentDirectoryEntry)),
    ];

    [Property]
    public Property No_change_keeps_any_reference() =>
        Prop.ForAll(ReferenceArbitraries.Details.ToArbitrary(), details => details.Apply(PartyReferenceChanges.None) == details);

    [Property]
    public Property The_changes_to_a_reference_give_that_reference_whatever_the_current_one() =>
        Prop.ForAll(
            ReferenceArbitraries.Details.ToArbitrary(),
            ReferenceArbitraries.Details.ToArbitrary(),
            (current, target) => current.Apply(ReferenceArbitraries.ChangesTo(target)) == target);

    [Property]
    public Property An_omitted_field_keeps_its_value_and_a_sent_one_takes_the_change() =>
        Prop.ForAll(
            ReferenceArbitraries.Details.ToArbitrary(),
            ReferenceArbitraries.PartialChanges.ToArbitrary(),
            (current, changes) =>
            {
                var updated = current.Apply(changes);
                return _fields.All(field => field.Change(changes) switch
                {
                    (false, _) => Equals(field.Value(updated), field.Value(current)),
                    (true, var value) => Equals(field.Value(updated), value),
                });
            });

    [Property]
    public Property Applying_the_same_changes_twice_is_applying_them_once() =>
        Prop.ForAll(
            ReferenceArbitraries.Details.ToArbitrary(),
            ReferenceArbitraries.PartialChanges.ToArbitrary(),
            (current, changes) => current.Apply(changes).Apply(changes) == current.Apply(changes));

    [Fact]
    public void Every_recorded_bian_reference_field_is_covered() =>
        // 24 BIAN UpdateReference fields, less the four staged for 1.21; GovernmentIssuedIdentityReference holds two values.
        _fields.Length.ShouldBe(24 - PartyReferenceFields.Staged.Count + 1);

    private static (bool Sent, object? Value) Of<TValue>(PartyReferenceChange<TValue>? change) =>
        (change is not null, change is null ? null : change.Value);
}

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
    public void An_update_with_names_and_residency_records_them_with_the_identifiers() =>
        EntrySpecification.Given(Registered())
            .When(UpdateReference(ReferenceChangesTo(FullReferenceDetails)))
            .Then(ReferenceUpdated(FullReferenceDetails));

    [Fact]
    public void A_partial_update_keeps_the_names_and_residency() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(FullReferenceDetails))
            .When(UpdateReference(PartyReferenceChanges.None with
            {
                Nationality = new PartyReferenceChange<CountryCode?>(CountryCode.From("BE")),
            }))
            .Then(ReferenceUpdated(FullReferenceDetails with { Residency = Residency with { Nationality = CountryCode.From("BE") } }));

    [Fact]
    public void An_update_with_addresses_and_contact_points_records_them_with_the_other_values() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(FullReferenceDetails))
            .When(UpdateReference(ReferenceChangesTo(CompleteReferenceDetails)))
            .Then(ReferenceUpdated(CompleteReferenceDetails));

    [Fact]
    public void A_partial_update_keeps_the_addresses_and_contact_points() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(CompleteReferenceDetails))
            .When(UpdateReference(PartyReferenceChanges.None with
            {
                EmailAddress = new PartyReferenceChange<EmailAddress?>(EmailAddress.TryCreate("j.dupont@example.be").Value),
            }))
            .Then(ReferenceUpdated(CompleteReferenceDetails with
            {
                ContactPoints = ContactPoints with { EmailAddress = EmailAddress.TryCreate("j.dupont@example.be").Value },
            }));

    [Fact]
    public void A_date_of_birth_after_today_is_refused()
    {
        var error = PartyReferenceDataDirectoryEntry.Decide(
                UpdateReference(PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(Today.AddDays(1)) }),
                new[] { Registered() }.Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve))
            .Error;

        error.ShouldBe(PartyReferenceErrors.DateOfBirthAfterToday);
        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull().Keys.ShouldBe(["DateofBirth"]);
    }

    [Fact]
    public void A_date_of_birth_of_today_is_accepted() =>
        EntrySpecification.Given(Registered())
            .When(UpdateReference(PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(Today) }))
            .Then(ReferenceUpdated(PartyReferenceDetails.Empty with { DateOfBirth = Today }));

    [Fact]
    public void A_recorded_date_of_birth_is_not_checked_again_when_it_is_kept() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(ReferenceDetails with { DateOfBirth = Today.AddDays(5) }))
            .When(UpdateReference(PartyReferenceChanges.None))
            .Then(ReferenceUpdated(ReferenceDetails with { DateOfBirth = Today.AddDays(5) }));

    [Fact]
    public void An_update_with_legal_standing_and_documents_records_them_with_the_other_values() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(CompleteReferenceDetails))
            .When(UpdateReference(ReferenceChangesTo(FullestReferenceDetails)))
            .Then(ReferenceUpdated(FullestReferenceDetails));

    [Fact]
    public void A_partial_update_keeps_the_legal_standing_and_documents() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(FullestReferenceDetails))
            .When(UpdateReference(PartyReferenceChanges.None with
            {
                PartyLegalStructure = new PartyReferenceChange<PartyLegalStructure?>(PartyLegalStructure.PublicLimitedCompany),
            }))
            .Then(ReferenceUpdated(FullestReferenceDetails with
            {
                LegalStanding = LegalStanding with { LegalStructure = PartyLegalStructure.PublicLimitedCompany },
            }));

    [Fact]
    public void A_customer_since_date_after_today_is_refused() =>
        Decide(PartyReferenceChanges.None with { CustomerSinceDate = new PartyReferenceChange<DateOnly?>(Today.AddDays(1)) })
            .Error.ShouldBe(PartyReferenceErrors.CustomerSinceDateAfterToday);

    [Fact]
    public void A_customer_since_date_of_today_is_accepted() =>
        EntrySpecification.Given(Registered())
            .When(UpdateReference(PartyReferenceChanges.None with { CustomerSinceDate = new PartyReferenceChange<DateOnly?>(Today) }))
            .Then(ReferenceUpdated(PartyReferenceDetails.Empty with
            {
                LegalStanding = PartyLegalStanding.Empty with { CustomerSince = Today },
            }));

    [Fact]
    public void A_customer_since_date_before_the_date_of_birth_in_the_same_request_is_refused() =>
        Decide(PartyReferenceChanges.None with
            {
                DateOfBirth = new PartyReferenceChange<DateOnly?>(DateOfBirth),
                CustomerSinceDate = new PartyReferenceChange<DateOnly?>(DateOfBirth.AddDays(-1)),
            })
            .Error.ShouldBe(PartyReferenceErrors.CustomerSinceDateBeforeDateOfBirth);

    [Fact]
    public void A_customer_since_date_before_the_recorded_date_of_birth_is_refused() =>
        Decide(
                PartyReferenceChanges.None with { CustomerSinceDate = new PartyReferenceChange<DateOnly?>(DateOfBirth.AddYears(-1)) },
                ReferenceUpdated(FullReferenceDetails))
            .Error.ShouldBe(PartyReferenceErrors.CustomerSinceDateBeforeDateOfBirth);

    [Fact]
    public void A_date_of_birth_after_the_recorded_customer_since_date_is_refused() =>
        Decide(
                PartyReferenceChanges.None with { DateOfBirth = new PartyReferenceChange<DateOnly?>(CustomerSince.AddDays(1)) },
                ReferenceUpdated(FullestReferenceDetails))
            .Error.ShouldBe(PartyReferenceErrors.CustomerSinceDateBeforeDateOfBirth);

    [Fact]
    public void A_customer_since_date_on_the_date_of_birth_is_accepted() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(FullReferenceDetails))
            .When(UpdateReference(PartyReferenceChanges.None with { CustomerSinceDate = new PartyReferenceChange<DateOnly?>(DateOfBirth) }))
            .Then(ReferenceUpdated(FullReferenceDetails with
            {
                LegalStanding = PartyLegalStanding.Empty with { CustomerSince = DateOfBirth },
            }));

    [Fact]
    public void A_political_exposure_description_without_a_type_is_refused() =>
        Decide(PartyReferenceChanges.None with
            {
                PoliticalExposureDescription = new PartyReferenceChange<PoliticalExposureDescription?>(LegalStanding.PoliticalExposureDescription),
            })
            .Error.ShouldBe(PartyReferenceErrors.PoliticalExposureDescriptionWithoutExposure);

    [Fact]
    public void A_political_exposure_description_with_no_exposure_is_refused() =>
        Decide(PartyReferenceChanges.None with
            {
                PoliticalExposureType = new PartyReferenceChange<PoliticalExposureType?>(PoliticalExposureType.NoExposure),
                PoliticalExposureDescription = new PartyReferenceChange<PoliticalExposureDescription?>(LegalStanding.PoliticalExposureDescription),
            })
            .Error.ShouldBe(PartyReferenceErrors.PoliticalExposureDescriptionWithoutExposure);

    [Fact]
    public void Declaring_no_exposure_while_keeping_the_recorded_description_is_refused() =>
        Decide(
                PartyReferenceChanges.None with
                {
                    PoliticalExposureType = new PartyReferenceChange<PoliticalExposureType?>(PoliticalExposureType.NoExposure),
                },
                ReferenceUpdated(FullestReferenceDetails))
            .Error.ShouldBe(PartyReferenceErrors.PoliticalExposureDescriptionWithoutExposure);

    [Fact]
    public void Declaring_no_exposure_and_clearing_the_description_is_accepted() =>
        EntrySpecification.Given(Registered(), ReferenceUpdated(FullestReferenceDetails))
            .When(UpdateReference(PartyReferenceChanges.None with
            {
                PoliticalExposureType = new PartyReferenceChange<PoliticalExposureType?>(PoliticalExposureType.NoExposure),
                PoliticalExposureDescription = new PartyReferenceChange<PoliticalExposureDescription?>(null),
            }))
            .Then(ReferenceUpdated(FullestReferenceDetails with
            {
                LegalStanding = LegalStanding with
                {
                    PoliticalExposureType = PoliticalExposureType.NoExposure,
                    PoliticalExposureDescription = null,
                },
            }));

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

    private static Result<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> Decide(
        PartyReferenceChanges changes,
        params PartyReferenceDataDirectoryEntryEvent[] history) =>
        PartyReferenceDataDirectoryEntry.Decide(
            UpdateReference(changes),
            history.Prepend(Registered()).Aggregate(PartyReferenceDataDirectoryEntry.Initial, PartyReferenceDataDirectoryEntry.Evolve));
}

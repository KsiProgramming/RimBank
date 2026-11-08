using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using RimBank.SharedKernel;
using EntryLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryCommand,
    RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntryEvent>;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The decider laws on the entry (ADR 0010). Each runs 100 command sequences.
public sealed class PartyReferenceDataDirectoryEntryLawsProperties
{
    private static readonly DateTimeOffset _openDate = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private static readonly Gen<PartyType> _partyTypes = Gen.Frequency(
        (4, Gen.Elements(PartyType.Person, PartyType.Organisation)),
        (1, Gen.Elements((PartyType)(-1), (PartyType)2, (PartyType)99)));

    // Two of each id and two open dates: sequences repeat Register often, so both acceptance and
    // party.party-already-registered are common, and an undefined party type now and then covers party.party-type-invalid.
    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _registers =
        from entryId in Pick(PartyReferenceDataDirectoryEntryId.From)
        from partyType in _partyTypes
        from openDate in Gen.Elements(_openDate, _openDate.AddDays(1))
        from demographicsId in Pick(DemographicsId.From)
        from partyReferenceId in Pick(PartyReferenceId.From)
        from bankRelationsId in Pick(BankRelationsId.From)
        select (PartyReferenceDataDirectoryEntryCommand)new RegisterPartyReferenceDataDirectoryEntry(
            entryId, partyType, openDate, demographicsId, partyReferenceId, bankRelationsId);

    // Updates before Register cover party.party-not-found, and a missing party type keeps the current one (roadmap 1.8).
    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _updates =
        from partyType in Gen.Frequency((3, _partyTypes.Select(type => (PartyType?)type)), (1, Gen.Constant((PartyType?)null)))
        from days in Gen.Choose(0, 60)
        select (PartyReferenceDataDirectoryEntryCommand)new UpdatePartyReferenceDataDirectoryEntryDetails(partyType, _openDate.AddDays(days));

    private static readonly Gen<string?> _texts = Gen.Elements<string?>(null, "", "B2", "Contoso Ltd");

    // A foreign Demographics id now and then covers party.behavior-qualifier-not-found, and clearing texts covers
    // party.demographics-not-recorded on a later Exchange (roadmap 1.9).
    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _demographicsUpdates =
        from demographicsId in Pick(DemographicsId.From)
        from classification in _texts
        from employer in _texts
        from employment in Gen.Elements<Employment?>(null, new Employment(null, null), new Employment("Contoso Ltd", "Clerk"))
        from history in _texts
        select (PartyReferenceDataDirectoryEntryCommand)new UpdateDemographicsDetails(
            demographicsId,
            new DemographicsChanges(classification, employment, history, employer, null));

    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _demographicsExchanges =
        from demographicsId in Pick(DemographicsId.From)
        from action in Gen.Frequency(
            (4, Gen.Elements(ExchangeActionType.Verify, ExchangeActionType.Reject)),
            (1, Gen.Constant((ExchangeActionType)99)))
        from days in Gen.Choose(0, 60)
        select (PartyReferenceDataDirectoryEntryCommand)new ExchangeDemographicsDetails(demographicsId, action, _openDate.AddDays(days));

    private static readonly LegalEntityIdentifier _lei = LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value;

    private static readonly TaxIdentificationNumber _taxId = TaxIdentificationNumber.TryCreate("FR12345678901").Value;

    private static readonly CountryCode _france = CountryCode.From("FR");

    private static readonly DateOnly _today = DateOnly.FromDateTime(_openDate.UtcDateTime);

    private static readonly PoliticalExposureDescription _exposureDescription = PoliticalExposureDescription.TryCreate("Mayor").Value;

    // A foreign Reference id now and then covers party.behavior-qualifier-not-found (roadmap 1.10), a date of birth or
    // customer-since date after today party.reference-invalid (1.18, 1.20), and so do a customer-since date before the
    // date of birth and a political exposure description without an exposure (1.20); each value is kept, cleared or set.
    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _referenceUpdates =
        from partyReferenceId in Pick(PartyReferenceId.From)
        from taxId in Gen.Elements(null, new PartyIdentifierChange<TaxIdentificationNumber>(null), new PartyIdentifierChange<TaxIdentificationNumber>(_taxId))
        from lei in Gen.Elements(null, new PartyIdentifierChange<LegalEntityIdentifier>(null), new PartyIdentifierChange<LegalEntityIdentifier>(_lei))
        from nationality in Gen.Elements(null, new PartyReferenceChange<CountryCode?>(null), new PartyReferenceChange<CountryCode?>(_france))
        from birthDays in Gen.Choose(-20000, 3)
        from dateOfBirth in Gen.Elements(null, new PartyReferenceChange<DateOnly?>(null), new PartyReferenceChange<DateOnly?>(_today.AddDays(birthDays)))
        from sinceDays in Gen.Choose(-20000, 3)
        from customerSince in Gen.Elements(null, new PartyReferenceChange<DateOnly?>(null), new PartyReferenceChange<DateOnly?>(_today.AddDays(sinceDays)))
        from exposure in Gen.Elements<PartyReferenceChange<PoliticalExposureType?>?>(
            null,
            new PartyReferenceChange<PoliticalExposureType?>(null),
            new PartyReferenceChange<PoliticalExposureType?>(PoliticalExposureType.Foreign),
            new PartyReferenceChange<PoliticalExposureType?>(PoliticalExposureType.NoExposure))
        from description in Gen.Elements(
            null,
            new PartyReferenceChange<PoliticalExposureDescription?>(null),
            new PartyReferenceChange<PoliticalExposureDescription?>(_exposureDescription))
        select (PartyReferenceDataDirectoryEntryCommand)new UpdatePartyReferenceDetails(
            partyReferenceId,
            new PartyReferenceChanges(
                taxId,
                null,
                lei,
                DateOfBirth: dateOfBirth,
                Nationality: nationality,
                PoliticalExposureType: exposure,
                PoliticalExposureDescription: description,
                CustomerSinceDate: customerSince),
            _today);

    private static readonly Gen<PartyReferenceDataDirectoryEntryCommand> _commands =
        Gen.OneOf(_registers, _updates, _demographicsUpdates, _demographicsExchanges, _referenceUpdates);

    [Property]
    public Property Decisions_are_deterministic() => EntryLaws.DecisionsAreDeterministic(_commands);

    [Property]
    public Property Decided_events_evolve() => EntryLaws.DecidedEventsEvolve(_commands);

    private static Gen<TId> Pick<TId>(Func<Guid, TId> from) =>
        Gen.Elements(from(Guid.CreateVersion7(_openDate)), from(Guid.CreateVersion7(_openDate.AddMinutes(1))));
}

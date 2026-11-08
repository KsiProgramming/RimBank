# Plan: Party Reference Data Directory: Reference BQ (names and residency)

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group).
- Test namespaces end with `.Tests`, and context namespaces stay flat (tech-stack §3.3, ADR 0026). SharedKernel tests are all in `RimBank.SharedKernel.Tests`.
- Use switch expressions only (IDE0066).
- Do not log any name, birth date, nationality or residency value (7.2).
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## 1. SharedKernel: `CountryCode` (ISO 3166-1 alpha-2)
- [ ] Tests first in `tests/RimBank.SharedKernel.Tests/Countries/`:
  - `CountryCodeTests`:
    - `fr` and ` FR ` parse to `FR`;
    - an unknown code (`ZZ`), a user-assigned code (`XK`), an alpha-3 code (`FRA`), an empty value and a non-letter value are invalid, with a stable error code;
    - equality and `ToString`.
  - `CountryCodeJsonTests`: written and read as a plain string; an invalid string fails to read.
  - `CountryCodeTableSnapshotTests` and `Iso3166.snapshot.csv`: `CountryCode.All` matches the snapshot exactly. This mirrors `CurrencyTableSnapshotTests`.
- [ ] `src/RimBank.SharedKernel/CountryCode.cs`, `CountryCode.Iso3166.cs` (the embedded table of officially assigned codes) and `CountryCodeJsonConverter.cs`, following the `Currency` pattern: private constructor, `FrozenDictionary` lookup, `All`, and `Create` returning `Result<CountryCode>`.
- [ ] Architecture tests stay green (SharedKernel uses the BCL only).

## 2. Core: value objects, changes, decision and projection
- [ ] Tests first in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyNameTests`:
    - the legal name is trimmed; 140 characters is accepted and 141 is invalid;
    - the salutation: 35 characters is accepted and 36 is invalid;
    - blank → clear.
  - `DateOfBirthTests` (parsing):
    - `1990-05-17` and `1990-05-17T23:30:00-05:00` → `1990-05-17`;
    - garbage → invalid.
  - `ResidencyStatusTests`:
    - `resident` → `Resident`;
    - an unknown code → invalid;
    - `StatusDateTime` without a code → invalid;
    - all parts empty → clear.
  - `PartyReferenceChangesTests` (extend):
    - each new field: `null` keeps, `""` or an all-empty object clears;
    - several invalid fields → one `party.reference-invalid` naming every path (`PartyFullLegalName`, `Nationality.Code`, `PartyCountryOfResidence.Code`, `ResidencyStatus.StatusDescription`, `DateofBirth`);
    - other `Country` parts (`Citizen`, …) and other `Status` parts (`StatusReason`, …) → `party.reference-field-not-supported`, with no phase;
    - the six 1.18 fields are no longer refused;
    - update the existing pin: **16** staged fields, across 1.19 and 1.20 only.
  - `UpdatePartyReferenceTests` (Given / When / Then on the entry decider):
    - the first Update with every 1.18 field → `PartyReferenceUpdated` with the full details;
    - a partial Update keeps the identifiers and other fields;
    - a `DateofBirth` after the command's `Today` → `party.reference-invalid` naming `DateofBirth`;
    - a birth date equal to today is accepted.
  - `UpdatePartyReferenceHandlerTests`: the handler stamps `Today` from a `FakeTimeProvider`.
  - `Retrieval/PartyReferenceDataDirectoryEntryViewProjectionTests` (Given events → Then view):
    - the new fields reach the Reference section;
    - a 1.10-shaped `PartyReferenceUpdated` (identifiers only) still projects, with the new parts empty.
  - `EntryExamples`: add the 1.18 sample values.
- [ ] Production code in `src/RimBank.Core/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyFullLegalName`, `PartyNameSalutation` (value objects); `PartyNames` (both, optional, `Empty`/`Apply`).
  - `ResidencyStatusCode` (closed set `Resident`/`NonResident`), `ResidencyStatus` (code + optional `StatusDateTime`).
  - `PartyResidency` (`Nationality`, `CountryOfResidence` as `CountryCode?`, `ResidencyStatus?`, `Empty`/`Apply`).
  - `PartyReferenceDetails` gains `Names`, `DateOfBirth` (`DateOnly?`) and `Residency`, as nullable members with `Current…` accessors. `Apply` merges them.
  - `PartyReferenceChanges` gains one change per field. `Create(...)` takes the new raw values and the presence flags for the `Country`/`Status` sub-parts, and still checks unsupported fields first.
  - `PartyReferenceFields`:
    - add the field path constants (`PartyFullLegalName`, `PartyNameSalutation`, `DateofBirth`, `Nationality.Code`, `PartyCountryOfResidence.Code`, `ResidencyStatus.StatusDescription`, `ResidencyStatus.StatusDateTime`);
    - remove the six `1.18` entries from `Staged`;
    - add the allowed `Country` and `Status` sub-part names.
  - `UpdatePartyReference` gains `Today` (`DateOnly`). `UpdatePartyReferenceHandler` sets it from `TimeProvider`. The decision refuses a future birth date.
  - `PartyReferenceUpdated` is unchanged in name, and its `Details` grows. Update the view projection.

## 3. PostgreSQL: event and view round-trip
- [ ] Tests first in `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/`, in a new `ReferenceRoundTripTests` or by extending `MartenPartyDirectoryTests` / `MartenPartyDirectoryEnquiryTests`:
  - an Update with every 1.18 field → the stream folds back to the same `PartyReferenceDetails`, and the inline view holds the same values, with `CountryCode` and `DateOnly` serialised as plain strings;
  - a stored 1.10 `PartyReferenceUpdated` JSON (no new members) still loads and folds;
  - a projection rebuild from the stream gives the same view.
- [ ] Register `CountryCodeJsonConverter` wherever `CurrencyJsonConverter` and the identifier converters are registered for the Party store, if they are not picked up by attribute.

## 4. HTTP adapter and conformance
- [ ] Tests first in `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyReferenceRequestTests`: BIAN JSON → presence flags and raw values, including `Nationality: { Code }`, `ResidencyStatus: { StatusDescription, StatusDateTime }` and unknown sub-parts reported as present.
  - `PartyReferenceResponseTests`: empty fields are left out; `DateofBirth` is `YYYY-MM-DD`; countries are `{ "Code": "FR" }`.
  - `UpdatePartyReferenceEndpointTests`:
    - 200 with the new `ETag` and the full body;
    - 400 `party.reference-invalid` (`application/problem+json`, `errors` keys as above) for a bad country, a long name, a future birth date and an unknown residency code;
    - 400 `party.reference-field-not-supported` for `eMailAddress` (1.19) and `Nationality.Citizen` (no phase);
    - an `If-Match` mismatch is still 409.
  - `RetrievePartyReferenceEndpointTests`: GET returns the 1.18 fields; 304 on a matching `If-None-Match`.
  - `ReferenceScenarios`: add the 1.18 sample payload.
- [ ] Production code in `src/RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`:
  - new `CountryModel` (`Code`) and `ResidencyStatusModel` (`StatusDescription`, `StatusDateTime`);
  - `UpdatePartyReferenceRequest` and `PartyReferenceResponse` gain the six fields;
  - the endpoint passes the raw values and sub-part presence to `PartyReferenceChanges.Create`, with no business rule in the adapter (memory: business logic in core).
- [ ] Conformance: there is no new route, so `PartyReferenceDataDirectoryCatalogue` is unchanged. Check that `PartyReferenceDataDirectorySpecificationTests` still passes with the larger body.

## 5. Docs and roadmap
- [ ] `docs/adr/0030-iso-3166-country-codes.md` (from `template.md`).
- [ ] Amend ADR 0029:
  - a "Recorded (1.18)" table;
  - the date-only `DateofBirth`;
  - the residency code set;
  - the refused sub-parts;
  - the staged table now holds 16 fields.
- [ ] Amend ADR 0024: the Reference paragraph.
- [ ] `specs/tech-stack.md`:
  - §3: the SharedKernel comment;
  - §3.4: the `Reference/` tree;
  - §7: the list of shared value objects.
- [ ] Tick **1.18** in `specs/roadmap.md`.
- [ ] Run `dotnet format --verify-no-changes`, then `dotnet build` and `dotnet test` (with Docker) for the whole solution.

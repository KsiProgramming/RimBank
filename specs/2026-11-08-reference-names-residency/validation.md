# Validation: Party Reference Data Directory: Reference BQ (names and residency)

## Done when (from roadmap)
> **1.18 Reference BQ: names and residency:** `PartyFullLegalName`, `PartyNameSalutation`, `DateofBirth`, `Nationality`, `PartyCountryOfResidence` (an ISO 3166 country value object) and `ResidencyStatus`, added to the Reference Update and Retrieve.
> + Definition of done (roadmap.md)

## Automated checks
- [ ] `RimBank.SharedKernel.Tests`:
  - `CountryCodeTests`, `CountryCodeJsonTests`;
  - `CountryCodeTableSnapshotTests`: the table matches `Iso3166.snapshot.csv`.
- [ ] `RimBank.Core.Tests` (`RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - `PartyNameTests`, `DateOfBirthTests`, `ResidencyStatusTests`;
  - `PartyReferenceChangesTests`: the new fields; the refused `Country`/`Status` sub-parts; the staged pin now at 16 fields across 1.19 and 1.20;
  - `UpdatePartyReferenceTests` (Given / When / Then): a future birth date is refused, today is accepted;
  - `UpdatePartyReferenceHandlerTests`: `Today` comes from `TimeProvider`.
- [ ] Projection test (Given events → Then view) in `PartyReferenceDataDirectoryEntryViewProjectionTests`:
  - the 1.18 fields reach the view;
  - a 1.10-shaped event still projects.
- [ ] `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (Testcontainers):
  - the stream and the inline view round-trip;
  - a stored 1.10 event still loads;
  - a projection rebuild gives the same view (DoD: read models rebuildable).
- [ ] `RimBank.Api.Tests` (Alba):
  - PUT 200 with `ETag`, and GET 200 / 304 with the new fields;
  - 400 `application/problem+json`:
    - `party.reference-invalid` with BIAN paths in `errors`;
    - `party.reference-field-not-supported` for a 1.19 field and for a `Country` sub-part;
  - 409 on a stale `If-Match`.
- [ ] `RimBank.Conformance.Tests` green (no new route; the Reference routes stay implemented).
- [ ] `RimBank.Architecture.Tests` green:
  - SharedKernel is BCL only;
  - no `double`/`float`;
  - flat namespaces;
  - no business rule in `.Api`.
- [ ] `dotnet format --verify-no-changes` clean, with no warnings (warnings are errors).
- [ ] GitHub Actions CI green: build, test, format, CodeQL.

## Manual / demo checks
- [ ] Run the host (`dotnet run --project src/RimBank.AppHost`), register a party (`POST /PartyReferenceDataDirectory/Register`), and keep the entry id, the `referenceid` and the `ETag`.
- [ ] `PUT /PartyReferenceDataDirectory/{id}/Reference/{referenceid}/Update` with `Idempotency-Key`, `If-Match` and:
  ```json
  {
    "PartyFullLegalName": "Jeanne Marie Dupont",
    "PartyNameSalutation": "Ms",
    "DateofBirth": "1990-05-17",
    "Nationality": { "Code": "fr" },
    "PartyCountryOfResidence": { "Code": "BE" },
    "ResidencyStatus": { "StatusDescription": "nonresident", "StatusDateTime": "2024-01-01T00:00:00Z" }
  }
  ```
  → 200, a new `ETag`, `Nationality.Code` = `FR`, `ResidencyStatus.StatusDescription` = `NonResident`, and the identifiers from earlier Updates unchanged.
- [ ] `GET …/Reference/{referenceid}/Retrieve` → 200 with the same values and `DateofBirth` = `1990-05-17`. Then send `If-None-Match` with that `ETag` → 304.
- [ ] Error cases, each a 400 `application/problem+json`:
  - `{"Nationality":{"Code":"ZZ"}}` → `party.reference-invalid`, `errors["Nationality.Code"]`;
  - a `DateofBirth` of tomorrow → `party.reference-invalid`, `errors["DateofBirth"]`;
  - `{"eMailAddress":"a@b.c"}` → `party.reference-field-not-supported`, naming 1.19.
- [ ] The host logs for these requests contain none of the submitted names, dates or country values.

## Merge criteria
- [ ] All checks above pass in CI.
- [ ] Roadmap checkbox 1.18 ticked in the same PR.
- [ ] ADR 0030 written; ADR 0029 and ADR 0024 amended; tech-stack §3, §3.4 and §7 updated.
- [ ] No PII in logs; no new error code without documentation (none is planned).
- [ ] The PR is opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<1.18 issue>`).

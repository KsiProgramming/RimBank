# Validation: Party Reference Data Directory: Reference BQ (addresses and contact points)

## Done when (from roadmap)
> **1.19 Reference BQ: addresses and contact points:** `ResidentialAddress`, `PartyAddress` and `CorporateAddress` (ISO 20022 postal address), and `eMailAddress`, `CellPhoneNumber` and `SocialNetworkContacts`, added to the Reference Update and Retrieve.
> + Definition of done (roadmap.md)

## Automated checks
- [ ] `RimBank.Core.Tests` (`RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - `PostalAddressTests`, `EmailAddressTests`, `MobileNumberTests`, `SocialNetworkAddressTests`;
  - `PartyReferenceChangesTests`:
    - the new fields and their paths;
    - the refused address, contact and `Country` parts;
    - the staged pin is now 10 fields, all tagged 1.20;
  - `PartyReferenceDetailsTests`: Apply merges, replaces and clears;
  - `UpdatePartyReferenceTests` (Given / When / Then): all six fields are recorded, and the 1.10 and 1.18 values are kept.
- [ ] Projection test (Given events → Then view) in `PartyReferenceDataDirectoryEntryViewProjectionTests`:
  - the 1.19 fields reach the view;
  - a 1.18-shaped event still projects.
- [ ] `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (Testcontainers):
  - the stream and the inline view round-trip;
  - a stored 1.18 event still loads;
  - a projection rebuild gives the same view (DoD: read models can be rebuilt).
- [ ] `RimBank.Api.Tests` (Alba):
  - PUT 200 with an `ETag`, and GET 200 / 304 with the new fields;
  - 400 `application/problem+json`:
    - `party.reference-invalid`, with the BIAN paths in `errors`;
    - `party.reference-field-not-supported` for a 1.20 field and for `ResidentialAddress.AddressType`;
  - 409 on a stale `If-Match`.
- [ ] `RimBank.Conformance.Tests` green: no new route, and the Reference routes stay implemented.
- [ ] `RimBank.Architecture.Tests` green:
  - no `double` or `float`;
  - flat namespaces;
  - no business rule in `.Api`;
  - the CQRS roles do not change.
- [ ] `dotnet format --verify-no-changes` clean, with no warnings (warnings are errors).
- [ ] GitHub Actions CI green: build, test, format, CodeQL.

## Manual / demo checks
- [ ] Run the host (`dotnet run --project src/RimBank.AppHost`) and register a party (`POST /PartyReferenceDataDirectory/Register`). Keep the entry id, the `referenceid` and the `ETag`.
- [ ] Send `PUT /PartyReferenceDataDirectory/{id}/Reference/{referenceid}/Update` with `Idempotency-Key`, `If-Match` and this body:
  ```json
  {
    "ResidentialAddress": {
      "StreetName": "Rue de Rivoli",
      "StreetBuildingIdentification": "12",
      "PostCodeIdentification": "75001",
      "TownName": "Paris",
      "Country": { "Code": "fr" }
    },
    "eMailAddress": { "EmailAddress": " jeanne@example.fr " },
    "CellPhoneNumber": { "MobileNumber": "+33 6 12 34 56 78" },
    "SocialNetworkContacts": { "URLAddress": "https://www.linkedin.com/in/jdupont" }
  }
  ```
  → 200, a new `ETag`, `ResidentialAddress.Country.Code` = `FR`, `MobileNumber` = `+33612345678`, the email trimmed, and the earlier 1.10 and 1.18 values unchanged.
- [ ] `GET …/Reference/{referenceid}/Retrieve` → 200 with the same values, and with no `PartyAddress` or `CorporateAddress` key. Then send `If-None-Match` with that `ETag` → 304.
- [ ] Error cases, each a 400 `application/problem+json`:
  - `{"PartyAddress":{"StreetName":"x","Country":{"Code":"BE"}}}` → `party.reference-invalid`, `errors["PartyAddress.TownName"]`;
  - `{"CellPhoneNumber":{"MobileNumber":"0612345678"}}` → `party.reference-invalid`, `errors["CellPhoneNumber.MobileNumber"]`;
  - `{"ResidentialAddress":{"AddressType":"HOME","TownName":"Paris","Country":{"Code":"FR"}}}` → `party.reference-field-not-supported`, naming `ResidentialAddress.AddressType` with no phase;
  - `{"PartyLegalStructure":"x"}` → `party.reference-field-not-supported`, naming 1.20.
- [ ] The host logs for these requests contain none of the submitted addresses, email addresses, phone numbers or URLs.

## Merge criteria
- [ ] All checks above pass in CI.
- [ ] Roadmap checkbox 1.19 ticked in the same PR.
- [ ] ADR 0031 written; ADR 0029 and ADR 0024 amended; tech-stack §3.4 updated.
- [ ] No PII in logs, and no new error code (none is planned).
- [ ] The PR is opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<1.19 issue>`).

# Validation: Party Reference Data Directory: Reference BQ (legal standing, political exposure and documents)

## Done when (from roadmap)
> **1.20 Reference BQ: legal, political exposure and corporate details:** `PartyLegalStructure`, `PoliticalExposureType`, `PoliticalExposureDescriptionRecord`, `CustomerSinceDate`, the corporate customer and legal entity references, the company officers and their roles, `GovernmentIssuedDocumentDetails` and `DocumentDirectoryEntryInstanceReference`, added to the Reference Update and Retrieve.
> + Definition of done (roadmap.md)

As agreed in the interview, the corporate customer, legal entity and officer references move to the new row **1.21**. 1.20 is done when its reworded row (the six own fields) is met.

## Automated checks
- [ ] `RimBank.Core.Tests` (`RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - value object tests: `PartyLegalStructureTests`, `PoliticalExposureTests`, `GovernmentIssuedDocumentTests`, `DocumentDirectoryEntryReferenceTests`;
  - `PartyReferenceChangesTests`:
    - the new fields and their paths;
    - the refused nested parts;
    - the staged pin is now **4 fields, all tagged 1.21**;
  - `PartyReferenceDetailsTests`: Apply merges, replaces and clears;
  - `UpdatePartyReferenceTests` (Given / When / Then):
    - all six fields are recorded, and the earlier values are kept;
    - the PEP-description invariant;
    - `CustomerSinceDate` is not in the future and not before `DateofBirth`.
- [ ] FsCheck `PartyReferencePartialUpdateProperties`: no-op, keep-omitted and idempotence laws over every Reference field.
- [ ] Projection test (Given events → Then view) in `PartyReferenceDataDirectoryEntryViewProjectionTests`:
  - the 1.20 fields reach the view;
  - a 1.19-shaped event still projects.
- [ ] `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (Testcontainers):
  - the stream and the inline view round-trip;
  - a stored 1.19 event still loads;
  - a rebuild gives the same view (DoD: read models can be rebuilt).
- [ ] `RimBank.Api.Tests` (Alba):
  - PUT 200 with an `ETag`, and GET 200 / 304 with the new fields;
  - 400 `application/problem+json`:
    - `party.reference-invalid`, with the BIAN paths in `errors`;
    - `party.reference-field-not-supported` for `CompanyOfficerRole` (names 1.21) and for `GovernmentIssuedDocumentDetails.PartyReference` (no phase);
  - 409 on a stale `If-Match`.
- [ ] `ReferenceLogPrivacyTests`: no submitted Reference value appears in any captured log entry, on the success, 400, 409 and Retrieve paths.
- [ ] `RimBank.Conformance.Tests` green: no new route.
- [ ] `RimBank.Architecture.Tests` green:
  - no `double` or `float`;
  - flat namespaces;
  - no business rule in `.Api`;
  - the CQRS roles do not change.
- [ ] `dotnet format --verify-no-changes` clean, with no warnings.
- [ ] GitHub Actions CI green: build, test, format, CodeQL.

## Manual / demo checks
- [ ] Run `dotnet run --project src/RimBank.AppHost`, then the new "Reference BQ" requests in `src/RimBank.Host/RimBank.Host.http`, in order.
- [ ] Send `PUT /PartyReferenceDataDirectory/{id}/Reference/{referenceid}/Update` with `Idempotency-Key`, `If-Match` and this body:
  ```json
  {
    "PartyLegalStructure": "private limited company",
    "PoliticalExposureType": "Politicalexposuredomestic",
    "PoliticalExposureDescriptionRecord": " Member of a regional council since 2021 ",
    "CustomerSinceDate": "2015-03-02T10:00:00+01:00",
    "GovernmentIssuedDocumentDetails": {
      "PartyDocumentType": "PersonalDocument",
      "DocumentReference": {
        "DocumentIdentification": { "IdentifierValue": "23AB45678" },
        "DocumentName": "Passport",
        "DocumentValidityPeriod": {
          "FromDateTime": { "DateTimeContent": "2023-04-01" },
          "ToDateTime": { "DateTimeContent": "2033-03-31" }
        }
      }
    },
    "DocumentDirectoryEntryInstanceReference": { "DocumentDirectoryEntryIdentification": "DOC-000123" }
  }
  ```
  Expected: 200 and a new `ETag`. `PartyLegalStructure` is `PrivateLimitedCompany`, `CustomerSinceDate` is `2015-03-02`, the description is trimmed, and the earlier Reference values are unchanged.
- [ ] `GET …/Reference/{referenceid}/Retrieve` returns 200 with the same values. Sending `If-None-Match` with that `ETag` returns 304.
- [ ] Error cases, each a 400 `application/problem+json`:
  - `{"PoliticalExposureType":"Nopoliticalexposure","PoliticalExposureDescriptionRecord":"x"}` → `party.reference-invalid`, `errors["PoliticalExposureDescriptionRecord"]`;
  - `{"CustomerSinceDate":"2999-01-01"}` → `party.reference-invalid`, `errors["CustomerSinceDate"]`;
  - `{"GovernmentIssuedDocumentDetails":{"PartyDocumentType":"PersonalDocument"}}` → `party.reference-invalid`, naming `…DocumentIdentification.IdentifierValue`;
  - `{"CompanyOfficerRole":{"Identification":"x"}}` → `party.reference-field-not-supported`, naming 1.21.
- [ ] The host logs for these requests contain none of the submitted values.

## Merge criteria
- [ ] All checks above pass in CI.
- [ ] Roadmap: 1.20 reworded and ticked, and 1.21 added after 1.11, in the same PR.
- [ ] ADR 0032 written, with the ownership rule (declared, not assessed) and the rejected option; ADR 0029 and ADR 0024 amended; tech-stack §3.4 updated.
- [ ] No PII in logs (proved by `ReferenceLogPrivacyTests`), and no new error code.
- [ ] The PR is opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<1.20 issue>`).

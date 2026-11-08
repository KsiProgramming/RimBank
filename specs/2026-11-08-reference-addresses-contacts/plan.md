# Plan: Party Reference Data Directory: Reference BQ (addresses and contact points)

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group).
- Test namespaces end with `.Tests`, and context namespaces stay flat (tech-stack §3.3, ADR 0026).
- Use switch expressions only (IDE0066).
- Business rules live in `RimBank.Core`. The adapters only translate the transport (memory: business logic in core).
- Do not log any address, email address, phone number or URL value (7.2).
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## As built (differences from the plan below)
- **Commits.** One per group: value objects → core and HTTP → PostgreSQL → docs.
- **Phone length.** `MobileNumber` accepts 7 to 15 digits in all. The plan and the requirements first gave different minimums, and the shortest numbers in use (Niue, +683 xxxx) have 7.
- **Shared country reading.** `PartyReferenceCountry.Read` (trim, upper-case, `CountryCode`) is shared by `Nationality`, `PartyCountryOfResidence` and every address country, so they give the same message.
- **Address errors.** `PostalAddress.TryCreate` names its parts relative to the address (`TownName`, `Country.Code`), and `PartyReferenceChanges` prefixes them with the BIAN field (`ResidentialAddress.TownName`). `PostalAddress.TextParts` lists the recorded parts with their lengths.
- **Names in code.** The contact point property of `PartyContactPoints` is `SocialNetwork`, and the change is `SocialNetworkAddress`. The HTTP request and response property for `eMailAddress` is `EMailAddress`.
- **Test examples.** `EntryExamples.FullReferenceDetails` stays the 1.18 example. The new `CompleteReferenceDetails` adds `Addresses` and `ContactPoints`. `ReferenceChangesTo(details)` sends the 1.19 parts only when the details have them, so the 1.18 tests keep their meaning.
- **Test files.** The contact point tests are in `EmailAddressTests`, `MobileNumberTests` and `SocialNetworkAddressTests`, one class per file. The staged-field examples in the 1.10 and 1.18 tests moved from `eMailAddress` to `PartyLegalStructure` (1.20).
- **JSON in tests.** The default `System.Text.Json` encoder writes `+` as `+`, so the response test compares the value, not the raw JSON text.
- **No new converter registration.** The contact points use the `PartyReferenceTextJsonConverter<T>` attribute, and `PostalAddress` is a plain record whose `CountryCode` has its own converter.

## 1. Core: value objects
- [x] Tests first in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PostalAddressTests`:
    - a full address parses, and every part is trimmed;
    - each part at its maximum length is accepted, and one character more is invalid, with the part's path:
      - 70 for `Department` and `SubDepartment`;
      - 35 for `StreetName`, `StreetBuildingIdentification`, `BuildingName` and `TownName`;
      - 16 for `Floor`, `PostOfficeBox` and `PostCodeIdentification`;
    - a missing or blank `TownName` is invalid, and so is a missing `Country.Code`;
    - an unknown country is invalid, and `fr` reads as `FR`;
    - all parts blank → clear.
  - `EmailAddressTests`:
    - `jeanne@example.fr` and ` jeanne@example.fr ` → `jeanne@example.fr`;
    - invalid: no `@`, two `@`, an empty local part, a domain without a dot, inner whitespace, a display name (`Jeanne <j@x.fr>`), and 257 characters.
  - `MobileNumberTests`:
    - `+33 6 12 34 56 78`, `+33-612345678` and `+33.6.12.34.56.78` → `+33612345678`;
    - invalid: no `+` (`0612345678`), `+0…`, letters, parentheses (`+33 (0)6 12 34 56 78`, because stripping them would keep the trunk `0` and give a wrong number), more than 15 digits, fewer than 7 digits.
  - `SocialNetworkAddressTests`:
    - an `https` URL is accepted;
    - invalid: `http`, a relative URL, `javascript:`, and 2049 characters.
- [x] Production code in `src/RimBank.Core/Party/PartyReferenceDataDirectory/Reference/`:
  - `PostalAddress` (a sealed record with nullable parts and a non-null `TownName` and `Country`);
  - `EmailAddress`, `MobileNumber` and `SocialNetworkAddress`, on the `IPartyReferenceText<T>` / `PartyReferenceTextJsonConverter<T>` pattern from 1.18, so each is a plain JSON string;
  - `PartyAddresses` (`Residential`, `Party`, `Corporate`; `Empty`, `Apply`, `HasAny`);
  - `PartyContactPoints` (`EmailAddress`, `MobileNumber`, `SocialNetwork`; `Empty`, `Apply`, `HasAny`).

## 2. Core changes, decision and projection, with the HTTP adapter (one commit)
Once the six fields leave `Staged`, an adapter that does not map them would drop them silently, so the core and HTTP go in together (as in 1.18).
- [x] Core tests first:
  - `PartyReferenceChangesTests` (extend):
    - each new field: `null` keeps, and an all-blank object clears;
    - several invalid parts → one `party.reference-invalid` that names every path (`ResidentialAddress.TownName`, `CorporateAddress.Country.Code`, `eMailAddress.EmailAddress`, `CellPhoneNumber.MobileNumber`, `SocialNetworkContacts.URLAddress`);
    - refused address parts (`AddressType`, `Block`, `ValidityPeriod`, …), refused contact parts (`Identification`, `PhoneNumber` inside `CellPhoneNumber`, …) and other `Country` parts inside an address → `party.reference-field-not-supported`, with no phase;
    - the six 1.19 fields are no longer refused;
    - update the pin to **10** staged fields, all tagged 1.20.
  - `PartyReferenceDetailsTests`:
    - Apply merges the addresses and contact points;
    - replacing one address keeps the other two;
    - clearing the last part sets the member back to `null`.
  - `UpdatePartyReferenceTests` (Given / When / Then):
    - an Update with all six fields → `PartyReferenceUpdated` with the full details;
    - a partial Update keeps the 1.10 and 1.18 values.
  - `Retrieval/PartyReferenceDataDirectoryEntryViewProjectionTests`:
    - the new fields reach the Reference section;
    - a 1.18-shaped `PartyReferenceUpdated` still projects, with the new parts empty.
  - `EntryExamples`: add the 1.19 sample values.
- [x] Core production code:
  - `PartyReferenceInput` gains `PostalAddressInput? ResidentialAddress`, `PartyAddress` and `CorporateAddress`, and `string? EmailAddress`, `MobileNumber` and `SocialNetworkUrl`. A blank string clears.
  - New `PostalAddressInput` record (raw parts plus `PartyCountryInput? Country`).
  - `PartyReferenceFields`:
    - the path constants for the new fields and parts;
    - the allowed `PostalAddress` part names and the allowed part name of each contact field;
    - remove the six `1.19` entries from `Staged`.
  - `PartyReferenceChanges` gains one `PartyReferenceChange<T>` per field. `Create` parses them after the unsupported-field check.
  - `PartyReferenceDetails` gains `Addresses` and `ContactPoints` (nullable), `CurrentAddresses` and `CurrentContactPoints` (`[JsonIgnore]`), and merges them in `Apply`.
  - The view projection copies the new parts. `PartyReferenceUpdated` keeps its name.
- [x] HTTP tests first in `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyReferenceRequestTests`:
    - BIAN JSON → raw values;
    - unknown address, contact and `Country` parts are reported as unsupported paths (`ResidentialAddress.AddressType`, `CellPhoneNumber.PhoneNumber`, …).
  - `PartyReferenceResponseTests`:
    - empty addresses, empty parts and empty contacts are left out;
    - the address country is `{ "Code": "FR" }`;
    - contacts are `{ "EmailAddress": … }`, `{ "MobileNumber": … }` and `{ "URLAddress": … }`.
  - `UpdatePartyReferenceEndpointTests`:
    - 200 with the new `ETag` and the full body (with the 1.18 values kept);
    - 400 `party.reference-invalid`, with the `errors` keys above, for a missing town, a bad email and a bad phone number;
    - 400 `party.reference-field-not-supported` for `PartyLegalStructure` (1.20) and for `ResidentialAddress.AddressType` (no phase);
    - a stale `If-Match` is still 409.
  - `RetrievePartyReferenceEndpointTests`: GET returns the 1.19 fields, and a matching `If-None-Match` gets 304.
  - `ReferenceScenarios`: add the 1.19 sample payload.
- [x] HTTP production code in `src/RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`:
  - new `PostalAddressModel` (the recorded parts plus `CountryModel Country`), `EmailAddressModel`, `MobileNumberModel` and `SocialNetworkContactModel`;
  - `UpdatePartyReferenceRequest` and `PartyReferenceResponse` gain the six fields;
  - the endpoint collects the unknown parts from the extension data, as 1.18 does for `Country` and `Status`, and passes the raw values to `PartyReferenceChanges.Create`.
- [x] Conformance: there is no new route, so `PartyReferenceDataDirectoryCatalogue` does not change. `PartyReferenceDataDirectorySpecificationTests` must still pass.

## 3. PostgreSQL: event and view round-trip
- [x] Extend `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/PartyReferenceRoundTripTests.cs`:
  - an Update with all six fields → the stream folds back to the same `PartyReferenceDetails`, and the inline view holds the same values, with the contacts and countries stored as plain strings;
  - a stored 1.18 `PartyReferenceUpdated` JSON (without the new members) still loads and folds;
  - a projection rebuild from the stream gives the same view.
- [x] Register no new converter if attributes cover the new types. Otherwise register them next to the identifier converters.

## 4. Docs and roadmap
- [x] `docs/adr/0031-reference-contact-points-and-addresses.md`, from `template.md`.
- [x] Amend ADR 0029:
  - a "Recorded (1.19)" table;
  - the staged table now holds 10 fields, all tagged 1.20;
  - the pin count in Consequences;
  - the PII list.
- [x] Amend ADR 0024: the Reference paragraph.
- [x] `specs/tech-stack.md` §3.4: the `Reference/` tree.
- [x] Tick **1.19** in `specs/roadmap.md`.
- [x] Add an "As built" section to this plan for any differences.
- [x] Run `dotnet format --verify-no-changes`, then `dotnet build` and `dotnet test` (with Docker) for the whole solution.

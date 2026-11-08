# Party Reference Data Directory: Reference BQ (addresses and contact points)

Roadmap phase: 1.19
Branch: plan/1.19-reference-addresses-contacts

## Context
1.10 built BIAN `Reference/Update` and `Reference/Retrieve`, and 1.18 added the names, the date of birth and the residency. These six BIAN Reference fields are still refused with `party.reference-field-not-supported`, tagged `1.19` ([ADR 0029](../../docs/adr/0029-reference-fields-in-stages.md)):
- `ResidentialAddress`, `PartyAddress`, `CorporateAddress`: ISO 20022 `PostalAddress`;
- `eMailAddress`, `CellPhoneNumber`, `SocialNetworkContacts`: ISO 20022 `ContactPoint`.

This phase records them. As in 1.18, the Reference record stays in the entry aggregate (ADR 0024). The phase reuses the existing routes, handlers, check order, stream version (`ETag` / `If-Match`), `PartyReferenceUpdated` event and inline view. It adds **no new operation, port, route or view**.

It depends on 1.10 and 1.18 (both done). The addresses reuse 1.18's `CountryCode` ([ADR 0030](../../docs/adr/0030-iso-3166-country-codes.md)). It unblocks 1.20, the last Reference stage, and the Reference MCP tools (1.17).

**A gap in BIAN.** In the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml), `ContactPoint` is the abstract ISO 20022 base type. Its properties (`Identification`, `BICAddress`, `RelatedParty`, …) do not include an email address, a phone number or a URL, and `GenericIdentification` is not defined. In ISO 20022, those values live on the `ContactPoint` subtypes:
- `ElectronicAddress.EmailAddress` and `ElectronicAddress.URLAddress`;
- `PhoneAddress.MobileNumber`.

RimBank reads the subtype's property inside the BIAN object. Mission "Source of truth" 3 requires an ADR for this interpretation: ADR 0031.

Drivers:
- Mission principles:
  - 3: value objects, no primitive obsession;
  - 4: event-sourced truth;
  - 5: commands acknowledge from the decided state, and queries read views;
  - 8: Problem Details;
  - 10: small steps.
- Quality bar "Security": addresses, email addresses and phone numbers are personal data.
- Tech-stack:
  - §3.4: addresses are context value objects in the `Reference/` folder, with a flat namespace (ADR 0026);
  - §5: routes, `If-Match`, `ETag` and `Idempotency-Key`;
  - §8.1–8.3: inline view, rebuildable.

**CQRS split:**
- **Command side:** `UpdatePartyReference` gains the new changes. The flow does not change: load the entry, check the entry, the BQ id and the version, run a pure `Decide`, append with the expected version, and acknowledge from the decided state.
- **Query side:** `RetrievePartyReference` reads the same **inline** `PartyReferenceDataDirectoryEntryView`. Its Reference section gains the new fields. There is no async projection.

## Scope
### In scope
- **`PostalAddress` (core, `Reference/`).** One value object, used for all three address fields. It records a structured subset of BIAN `PostalAddress`, with BIAN's own length formats:

  | BIAN part | Rule |
  |---|---|
  | `Department`, `SubDepartment` | Max70Text |
  | `StreetName`, `StreetBuildingIdentification`, `BuildingName` | Max35Text |
  | `Floor`, `PostOfficeBox`, `PostCodeIdentification` | Max16Text |
  | `TownName` | Max35Text, **required** |
  | `Country.Code` | `CountryCode` (trimmed and upper-cased, as in 1.18), **required** |

  - Every part is trimmed, and a blank part is not recorded.
  - Required parts follow the CBPR+ structured-address rule, under which a structured address has at least a town and a country.
  - Any other `PostalAddress` part with a value (`AddressType`, `Location`, `ChequeIssue`, `ValidityPeriod`, `SuiteIdentification`, `BuildingIdentification`, `MailDeliverySubLocation`, `Block`, `Lot`, `MailingInstructions`, `PhysicalDelivery`) is refused with `party.reference-field-not-supported`, and no phase is named. Other `Country` parts are refused the same way, as in 1.18.
- **Contact points (core, `Reference/`).** Each field keeps BIAN's object shape and reads one ISO 20022 subtype property (ADR 0031):

  | BIAN field | Part read | Value object | Rule |
  |---|---|---|---|
  | `eMailAddress` | `EmailAddress` | `EmailAddress` | Trimmed, at most 256 characters. Exactly one `@`, a non-empty local part, a domain with at least one dot, no whitespace and no display name. Stored as written |
  | `CellPhoneNumber` | `MobileNumber` | `MobileNumber` | Trimmed, with spaces, hyphens and dots removed, then E.164 (`+`, a non-zero digit, 7 to 15 digits in all; the shortest numbers in use, such as Niue +683 xxxx, have 7). Stored normalised, for example `+33612345678`. This also accepts ISO 20022's `+33-612345678` |
  | `SocialNetworkContacts` | `URLAddress` | `SocialNetworkAddress` | Trimmed, at most 2048 characters, an absolute `https` URL. Stored as written |

  - Any other part of a contact object (`Identification`, `BICAddress`, `TemporaryIndicator`, and so on, or another subtype's property such as `PhoneNumber` or `EmailAddress` inside `CellPhoneNumber`) is refused with `party.reference-field-not-supported`, and no phase is named.
  - Each field records one current value, because BIAN gives a single `ContactPoint`.
- **Partial update (as in 1.18):**
  - an omitted field keeps its value;
  - a given object replaces the whole value;
  - an object whose recorded parts are all blank clears it;
  - every accepted Update appends `PartyReferenceUpdated` with the full resulting Reference.
- **Errors:** every invalid value goes into one `party.reference-invalid` (400). Its `errors` name each offending path, for example:
  - `ResidentialAddress.TownName` (missing or too long);
  - `CorporateAddress.Country.Code`;
  - `eMailAddress.EmailAddress`;
  - `CellPhoneNumber.MobileNumber`;
  - `SocialNetworkContacts.URLAddress`.

  The unsupported-field check still runs first, so a request is never applied in part.
- **Staged table:** the six `1.19` entries are removed from `PartyReferenceFields.Staged`. Ten fields remain, all tagged 1.20.
- **Model:**
  - `PartyReferenceDetails` gains `Addresses` (`PartyAddresses`: residential, party and corporate, each `PostalAddress?`) and `ContactPoints` (`PartyContactPoints`), with `CurrentAddresses` and `CurrentContactPoints` accessors.
  - Both members are nullable and `[JsonIgnore]` computed, as in 1.18, so 1.10 and 1.18 events and view documents still load and fold.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - new models: `PostalAddressModel`, `EmailAddressModel`, `MobileNumberModel` and `SocialNetworkContactModel`;
  - the Update request and the shared Update/Retrieve response gain the six fields;
  - an empty field or part is left out of the response;
  - the adapter only reports the values and the unknown parts it was sent. The rules stay in the core.
- **Docs:**
  - new ADR 0031;
  - ADR 0029: the 1.19 rows move to "recorded";
  - ADR 0024: the Reference paragraph;
  - tech-stack §3.4: the `Reference/` tree;
  - tick roadmap 1.19.

### Out of scope
- Legal structure, political exposure, corporate details and documents: **1.20**. They stay refused.
- The `AddressType` code and the other refused address parts. No phase is planned. A later ADR can add them.
- Address history and validity periods, more than one value per contact field, and email or phone verification (no OTP and no Exchange). BIAN gives Reference no Exchange.
- Rules tied to `PartyType`, such as a `CorporateAddress` for organisations only. As in 1.18, every field is accepted for both party types.
- Postal-code formats per country, ISO 3166-2 subdivisions, and checks that a phone number is a mobile number. `MobileNumber` is checked only as an E.164 number.
- FsCheck properties, `.http` demo files and a log-capture PII test. These were not chosen in the interview.
- The `Reference/Created` and `Reference/Updated` channels (ADR 0024, after 7.2), the MCP tools (**1.17**), per-operation scopes (**1.15**), and PII masking and encryption at rest (**7.2**). New code must not log these values.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Overall shape | Lean and ISO 20022 shaped | Chosen in the interview. Keeps BIAN's JSON objects, records only the parts with business meaning, and refuses the rest, so a 200 never hides an unstored value (ADR 0029) |
| Address parts | Department, SubDepartment, StreetName, StreetBuildingIdentification, BuildingName, Floor, PostOfficeBox, PostCodeIdentification, TownName, Country.Code | The structured parts that ISO 20022 payment messages (pacs/pain `PostalAddress`) use, so Payments (5.x) can reuse the same shape in its own context |
| Required address parts | `TownName` and `Country.Code` | The CBPR+ structured-address rule. An address that cannot locate a town is not useful to a bank |
| `AddressType` | Refused, with no phase | The BIAN field name (residential, party or corporate) already says what the address is for |
| Where `PostalAddress` lives | The Party core, `Reference/` | Tech-stack §3.4 lists addresses as context value objects. Payments will own its own address type (principle 7). `CountryCode` is already shared |
| Contact-point carrier | The ISO 20022 subtype property inside BIAN's object: `EmailAddress`, `MobileNumber`, `URLAddress` | BIAN's `ContactPoint` has no field for the value. This is the ISO 20022 meaning, and the BIAN JSON shape is kept. Recorded in ADR 0031 |
| Phone format | E.164, normalised on read | The international standard. It is also used to check numbers and send SMS, and the ISO 20022 hyphen form still parses |
| Email check | A syntax check only, at most 256 characters | Delivery can only be proven by verification, which is out of scope |
| Social network | One `https` URL | BIAN gives a single `ContactPoint`. A URL identifies a profile on any network |
| Update semantics | Partial: omitted keeps, an all-blank object clears, a given object replaces | Same as 1.9, 1.10 and 1.18 |
| Event | `PartyReferenceUpdated`, with the same name and a larger `Details` | Event-carried state. The new members are nullable, so earlier events still fold |
| Error codes | No new code: `party.reference-invalid` and `party.reference-field-not-supported` are reused | Same contract as 1.10 and 1.18 |
| Existing view documents | No migration. A missing part reads as empty, and rebuild is still proven | The shape only grows |
| Work order | Inside-out, test first: core value objects → changes, decision, projection and HTTP (one commit) → PostgreSQL → docs | Chosen in the interview. Core and HTTP go in one commit, so no commit drops fields silently (lesson from 1.18) |

## ADRs to write
- **ADR 0031 (new): Reference contact points and postal addresses.** It records:
  - the `ContactPoint` gap in BIAN, and the reading of the subtype properties (`EmailAddress`, `MobileNumber`, `URLAddress`);
  - the E.164 normalisation, the email syntax rule and the `https` rule;
  - the recorded address subset, the required town and country, and the refused parts;
  - why `PostalAddress` lives in the Party core and not in SharedKernel.

  Options considered: plain-string contact fields; `ContactPoint.Identification` as the carrier; the full `PostalAddress` with `AddressType` and `ValidityPeriod`; splitting the contacts into their own phase.
- **Amend ADR 0029:**
  - a "Recorded (1.19)" table;
  - the staged table now holds 10 fields, all tagged 1.20;
  - the `PartyReferenceChangesTests` pin count in Consequences;
  - the PII list.
- **Amend ADR 0024:** the "Reference requests" paragraph gains the 1.19 fields.
- **Tech-stack §3.4:** the `Reference/` tree gains `PostalAddress.cs`, `PartyAddresses.cs`, `EmailAddress.cs`, `MobileNumber.cs`, `SocialNetworkAddress.cs` and `PartyContactPoints.cs`.

## Open questions
- None.

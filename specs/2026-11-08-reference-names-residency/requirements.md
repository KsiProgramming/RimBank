# Party Reference Data Directory: Reference BQ (names and residency)

Roadmap phase: 1.18
Branch: plan/1.18-reference-names-residency

## Context
1.10 built BIAN `Reference/Update` and `Reference/Retrieve` for the party identifiers. It also refuses the other 22 BIAN Reference fields with `party.reference-field-not-supported`, each one tagged with the phase that will record it ([ADR 0029](../../docs/adr/0029-reference-fields-in-stages.md)). This phase is the first of those stages. It records the six fields tagged `1.18`:
- `PartyFullLegalName`
- `PartyNameSalutation`
- `DateofBirth`
- `Nationality`
- `PartyCountryOfResidence`
- `ResidencyStatus`

The Reference record still lives in the entry aggregate (ADR 0024). The phase therefore reuses 1.10's routes, handlers, check order, stream version (`ETag` / `If-Match`), `PartyReferenceUpdated` event and inline view. It adds **no new operation, port, route or view**. It only adds fields to the Reference record, the event payload and the response.

It depends on 1.10 (done). It unblocks 1.19, which needs the country value object for postal addresses, and the Reference MCP tools (1.17).

Drivers:
- Mission principles:
  - 3: value objects, no primitive obsession;
  - 4: event-sourced truth;
  - 5: commands acknowledge from the decided state, queries read views;
  - 8: Problem Details;
  - 10: small steps.
- Quality bar "Security": names, birth date and nationality are personal data.
- Tech-stack:
  - §3.4: the `Reference/` folder, flat namespace (ADR 0026);
  - §5: routes, `If-Match`, `ETag`, `Idempotency-Key`;
  - §7: shared value objects live in SharedKernel, context ones in the BQ folder;
  - §8.1–8.3: inline view, rebuildable.
- BIAN source: [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml), the `UpdateReference` / `RetrieveReference` schemas:
  - `Name` (string): `PartyFullLegalName`, `PartyNameSalutation`;
  - `ISODateTime`: `DateofBirth`;
  - `Country` (an object whose `Code` is the ISO 3166 code): `Nationality`, `PartyCountryOfResidence`;
  - `Status` (the large ISO 20022 object): `ResidencyStatus`.

**CQRS split:**
- **Command side:** `UpdatePartyReference` gains the new changes. The flow is unchanged: load the entry, check the entry, the BQ id and the version, run a pure `Decide`, append with the expected version, and acknowledge from the decided state.
- **Query side:** `RetrievePartyReference` reads the same **inline** `PartyReferenceDataDirectoryEntryView`, whose Reference section gains the new fields. There is no async projection.

## Scope
### In scope
- **`CountryCode` (SharedKernel, next to `Currency`).** ISO 3166-1 alpha-2, officially assigned codes only, from an embedded table.
  - Looked up in the table by the exact code, like `Currency`. The Party core trims and upper-cases client input first. An unknown code is invalid.
  - `All`, `TryParse`/`Create` returning `Result`, and a JSON converter that writes a plain string (as `Currency`, ADR 0009).
  - A checked-in snapshot CSV pins the table, as `Iso4217.snapshot.csv` does for currencies.
- **Name value objects (core, `Reference/`):**
  - `PartyFullLegalName`: trimmed, not blank, at most 140 characters (Max140Text).
  - `PartyNameSalutation`: trimmed, not blank, at most 35 characters (Max35Text).
- **Date of birth:**
  - The value is stored as a `DateOnly`.
  - Accepted forms: an ISO date (`1990-05-17`), or an ISO date-time. For a date-time, the calendar date as written is kept, with no time-zone conversion.
  - A date after today is invalid. "Today" comes from `TimeProvider`: the handler stamps it on the command, and the decision checks it.
- **Country fields:**
  - `Nationality` and `PartyCountryOfResidence` keep BIAN's object shape, `{ "Code": "FR" }`.
  - `Code` is parsed as a `CountryCode`.
  - Any other `Country` part with a value (`DomiciledFunds`, `Citizen`, `Tax`, …) is `party.reference-field-not-supported`, with no phase named, as for `OtherIdentification` in 1.10.
- **`ResidencyStatus`:**
  - `{ "StatusDescription": "Resident" | "NonResident", "StatusDateTime": "<ISO date-time>"? }`.
  - The code is a closed set, matched case-insensitively and stored normalised. `StatusDateTime` is optional, and is only allowed together with a code.
  - Any other `Status` part with a value is `party.reference-field-not-supported`, with no phase named.
  - Only the current value is recorded, with no history ("history as appropriate" is not modelled).
- **Partial update (as in 1.9 and 1.10):**
  - an omitted field keeps its value;
  - `""` clears a string field;
  - a given `Country` or `ResidencyStatus` object replaces the whole value, and one whose parts are all empty clears it (as `Employment` in 1.9);
  - every accepted Update appends `PartyReferenceUpdated` with the full resulting Reference.
- **Errors:** every invalid value goes into one `party.reference-invalid` (400), with every offending BIAN field path named in `errors`:
  - `PartyFullLegalName`, `Nationality.Code`, `ResidencyStatus.StatusDescription`, and so on;
  - a future `DateofBirth`, which the decision reports with the same code.
- **Staged table:** remove the six `1.18` entries from `PartyReferenceFields.Staged`. 16 fields remain, across 1.19 and 1.20.
- **Model:**
  - `PartyReferenceDetails` gains the new parts: names, date of birth, and residency (nationality, country of residence, residency status).
  - The new members are nullable, so 1.10 events and view documents still deserialize and fold. A missing part reads as empty.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - the Update request and the shared Update/Retrieve response gain the six BIAN fields;
  - an empty field is left out of the response;
  - `DateofBirth` is answered as `YYYY-MM-DD`.
- **Docs:**
  - ADR 0029: the 1.18 rows move from "staged" to "recorded", with their rules;
  - ADR 0024: the Reference paragraph;
  - new ADR 0030 for `CountryCode`;
  - tech-stack §3.4 tree and the SharedKernel comment in §3;
  - tick roadmap 1.18.

### Out of scope
- Addresses and contact points (`ResidentialAddress`, `PartyAddress`, `CorporateAddress`, `eMailAddress`, `CellPhoneNumber`, `SocialNetworkContacts`): **1.19**. They stay refused.
- Legal structure, political exposure, corporate details and documents: **1.20**. They stay refused.
- Rules tied to `PartyType`, such as no birth date or salutation for an Organisation: not chosen. Every field is accepted for both party types.
- History of nationality or residency, and multiple nationalities: not chosen. BIAN's single `Country` is recorded as the current value.
- ISO 3166-1 user-assigned or exceptionally reserved codes (for example `XK`), alpha-3 and numeric codes, and subdivisions (ISO 3166-2): not chosen.
- A lower bound on the birth date: not chosen (see Open questions).
- FsCheck properties, a separate staged-table pin test (the existing pin is updated to 16) and `.http` demo files: not chosen in the interview.
- The `Reference/Created`/`Updated` channels (ADR 0024, after 7.2), MCP tools (**1.17**), per-operation scopes (**1.15**), and PII masking and encryption at rest (**7.2**). New code must not log these values.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Overall shape | Lean and BIAN-shaped | Chosen in the interview. Keeps BIAN's JSON shapes, models only the parts with business meaning, and refuses the rest |
| Country value object | `CountryCode` in SharedKernel, ISO 3166-1 alpha-2, embedded table pinned by a snapshot | Chosen in the interview. 1.19 (postal addresses) and Payments (5.x, ISO 20022 parties) need it too, and tech-stack §7 puts shared value objects in SharedKernel. Mirrors `Currency` (ADR 0009). Recorded in ADR 0030 |
| Country JSON | BIAN `{ "Code": "FR" }` in requests and responses, plain `"FR"` in events and views | Follows the BIAN schema at the edge, keeps the stored form simple (like `Currency`) |
| Other `Country` / `Status` parts | Refused with `party.reference-field-not-supported`, no phase named | A 200 must never hide data that was not stored (ADR 0029) |
| Names | Trimmed, not blank; legal name at most 140, salutation at most 35 characters | ISO 20022 Max140Text / Max35Text. BIAN's `Name` gives no length |
| Date of birth | `DateOnly`. An ISO date or the date part of a date-time is accepted, and a date after today is refused. Answered as `YYYY-MM-DD` | A birth date is a calendar date: time and offset carry no meaning. "Today" comes from `TimeProvider` through the handler, so `Decide` stays pure. The difference from BIAN's `ISODateTime` is recorded in ADR 0029 |
| Residency status | Closed code `Resident` / `NonResident` in `Status.StatusDescription`, plus an optional `StatusDateTime` | Chosen in the interview. These are the values with banking meaning (tax, regulatory reporting), stored in BIAN's own `Status` object |
| History | Current value only | Chosen in the interview. Events keep the past anyway (principle 4) |
| Update semantics | Partial: omitted keeps, `""` or an all-empty object clears, a given object replaces | Same as 1.9 and 1.10 |
| Event | `PartyReferenceUpdated` is unchanged in name, and its `Details` grows | Event-carried state. New members are nullable, so 1.10 events still fold |
| Error codes | No new code: `party.reference-invalid` and `party.reference-field-not-supported` are reused | Same contract as 1.10 |
| Existing view documents | No migration. Missing parts read as empty, and rebuild is still proven | The shape only grows |
| Work order | Inside-out, test first: SharedKernel `CountryCode` → core → Postgres → HTTP and conformance → docs and roadmap, one commit per group | Chosen in the interview |

## ADRs to write
- **ADR 0030 (new): ISO 3166 country codes in SharedKernel.** It records:
  - alpha-2, officially assigned codes only;
  - the embedded table and its snapshot test;
  - parsing (trim, upper-case);
  - the JSON form (a plain string);
  - why the type is in SharedKernel and not the Party core.
  - Options considered: in the Party core until 1.19; a third-party package; alpha-3.
- **Amend ADR 0029:**
  - move the 1.18 fields into a "Recorded (1.18)" table with their value objects and rules;
  - the `DateofBirth` date-only interpretation;
  - the `ResidencyStatus` code set;
  - the refused `Country` and `Status` sub-parts;
  - the staged count drops to 16.
- **Amend ADR 0024:** the "Reference requests" paragraph gains the 1.18 fields.
- **Tech-stack:**
  - §3: the SharedKernel comment adds `CountryCode`;
  - §3.4: the `Reference/` tree;
  - §7: `CountryCode` added to the list of shared value objects.

## Open questions
- **Lower bound on `DateofBirth`.** Built without one: only "not in the future" is checked (recorded in ADR 0029). A floor such as 1900-01-01 would catch typos, but it is a business rule the user has not chosen, and adding it later is a small change.

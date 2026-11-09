# 0037. BIAN body, status and payload conformance

- Status: Accepted
- Date: 2026-11-09
- Roadmap phase: 1.14

## Context

Roadmap 1.14 asks for "OpenAPI and AsyncAPI conformance tests green for the implemented subset". Since 0.14 the harness ([ADR 0020](0020-bian-conformance-harness.md)) has checked routes, methods, the command/query rule and channel addresses. It left out request and response bodies. [ADR 0036](0036-party-published-language.md) also said 1.14 would compare the camelCase wire names of the published events to BIAN's PascalCase names.

The mission's source-of-truth rule and "no speculative coverage" mean RimBank implements a **subset** of each BIAN schema and does not invent fields. But earlier ADRs deliberately did differ from BIAN in some places: response bodies carry record ids, Demographics records a verification, contact points use their ISO 20022 subtypes, and events carry an envelope. A body check has to keep those differences visible and stop new, undecided ones.

Three things about the BIAN v14 files matter here (`doc-0/` is read-only, [ADR 0002](0002-bian-specs-location.md)):

- In the OpenAPI, request and response bodies are under the media-type key `'application/json:'`, with a stray colon.
- In the Party OpenAPI, the `200` of the Control Record's Register, Control, Update, Request, Execute and Exchange operations points to `#/components/responses/BadRequest` (an `HTTPError` body). Each operation's own response component, `<Action><Record>Response` (for example `RegisterPartyReferenceDataDirectoryEntryResponse`), exists but is not used.
- Enums are written as generated wrappers in the OpenAPI (`Partytypevalues: { Partytypevalues: Person | Organisation }`, which [ADR 0024](0024-party-reference-data-directory-mapping.md) flattens to strings), but as plain string schemas in the AsyncAPI. Many ISO 20022 entity references are `type: string` with a `format` that names a type.

## Options considered

How RimBank's shape is read:

1. **Reflection over the CLR models.** Simple, but it guesses property names, ignore conditions and `JsonElement` passthroughs.
2. **`JsonSchemaExporter` with the serializer options the host and the bus actually use.** It reads the real serializer contract, and it is the exporter 1.13 already uses for its schema snapshots.
3. **The host's generated OpenAPI document.** It is not compared yet (ADR 0020), and it describes the Problem Details bodies, which are out of scope.

How deliberate differences are recorded:

1. **Rules built into the harness** (for example "an `<X>Id` property is always allowed"). The list is shorter, but a difference passes without anyone deciding it.
2. **A list per context, one row per JSON path, each citing its ADR.** Longer, but every difference is reviewed and traced to a decision.

## Decision

- **Rule:** for every implemented operation, RimBank's request body and each of its 2xx response bodies are a subset of the BIAN schema, checked by JSON path. Each property RimBank writes must exist at the same path, and its JSON type must be compatible. Having fewer properties is always fine. For every published integration event, the payload is a subset of its channel's message payload in the same way.
- **Shapes:** RimBank's schema comes from `JsonSchemaExporter` (option 2). For HTTP bodies it uses the host's `Microsoft.AspNetCore.Http.Json.JsonOptions`, and for events Wolverine's default message settings (camelCase). The request type is the one minimal APIs infer from the body parameter (`IAcceptsMetadata`). The responses come from `.Produces<T>(status)` (`IProducesResponseTypeMetadata`).
- **Matching:**
  - HTTP property names must match exactly. Event property names are compared without regard to case (ADR 0036).
  - Only JSON types are compared: `object`, `array`, `string`, `number`, `integer` and `boolean`. RimBank may write an `integer` where BIAN declares a `number`. `format`, `required` and `enum` values are not compared, because validation is a business rule in core.
  - A BIAN enum wrapper accepts a plain string (ADR 0024).
  - A schema without a type allows anything, on either side. For RimBank that is a `JsonElement` passthrough or a recursive `$ref`.
  - `null` is always allowed. A number that the web defaults can also read from a string still counts as a number.
- **Statuses:** each endpoint must declare at least one 2xx response, and each 2xx it declares must be a 2xx the spec declares for that operation. Error statuses are not compared, because Problem Details replaces BIAN's `HTTPError` ([ADR 0015](0015-problem-details-mapping.md)) and RimBank adds 409 and 428 by design (ADR 0018, [ADR 0027](0027-optimistic-concurrency-for-commands.md)). A `304` is not a 2xx and is not compared either.
- **Request presence:** an endpoint reads a JSON body exactly when the spec declares one, so a missing `.Accepts`/body parameter cannot silently skip the check.
- **Spec quirks:** a media type is JSON when its key starts with `application/json`. When a 2xx response resolves to `HTTPError`, the harness uses the operation's own `#/components/responses/<Action><Record>Response`.
- **Deviations as data** (option 2): `ConformanceDeviation(Target, Part, JsonPath, Reason)`, where `Part` is `Request`, `Response` or `Payload`. A listed row excuses exactly the problem at that path. A row that excuses nothing on a checked operation or channel fails. A row whose reason does not cite an ADR (`ADR NNNN`) also fails. Each context keeps its list in `RimBank.Api.Tests`, next to its conformance test.
- **Declarations come from the catalogue:** each context's ADR catalogue (operations and channels, with their phase and event type) moves into `tests/RimBank.Conformance.Testing/Catalogues/`. The context's conformance test declares its operations (`Implemented`) and events (`Published`) as the catalogue's rows that are not deferred. A test library is not a production assembly, so its reference to the context's `.PublishedLanguage` breaks no dependency rule.
- **API:** `endpoints.ShouldHaveBodiesConformingTo(specification, jsonOptions, deviations)` with `ImplementedEndpoint.From(EndpointDataSource)`, and `publishedEventTypes.ShouldHavePayloadsConformingTo(specification, wireOptions, deviations)`. As in ADR 0020, every problem is reported in one failure, with the operation or channel, the JSON path and how to fix or excuse it.

The Party Reference Data Directory deviations at the time of this ADR (`PartyReferenceDataDirectoryConformanceDeviations`) are:

| Where | Paths | Decided in |
|---|---|---|
| Responses of every operation | the ids of the records they are about (`PartyReferenceDataDirectoryEntryId`, `DemographicsId`, `ReferenceId`, `BankRelationsId`, `AssociationsId`) | ADR 0024 |
| Entry Retrieve and Update responses | `RefreshDate` | ADR 0024 |
| Demographics responses; Exchange request | `VerificationStatus`, `VerificationDate`; `ExchangeActionType` | [ADR 0028](0028-demographics-exchange-as-verification.md) |
| Reference Update request and Reference responses | `ResidentialAddress.Country` as BIAN's `Country` object; `eMailAddress.EmailAddress`, `CellPhoneNumber.MobileNumber`, `SocialNetworkContacts.URLAddress` | [ADR 0030](0030-iso-3166-country-codes.md), [ADR 0031](0031-reference-contact-points-and-addresses.md) |
| Reference responses | `CompanyOfficerRole` as a string | [ADR 0034](0034-reference-corporate-and-officer-references.md) |
| `PartyReferenceDataDirectoryEntry/Created` and `…/Updated` payloads | `eventId`, `occurredAt`, `partyReferenceDataDirectoryEntryId`, `version` | ADR 0036 |

No Party model had to change: every difference the check found had already been decided in one of these ADRs.

## Consequences

- A new field that BIAN does not declare, a renamed BIAN field, a retyped field or a new success status fails the context's conformance test. The pull request then either fixes the model or adds a deviation row and the ADR behind it.
- Each new BIAN operation or channel is added to the context's catalogue, with its phase, in the same PR that implements it. The conformance tests then pick it up with no hand-written list.
- MCP tools reuse the `.Api` request models (tech-stack §6), so from 1.16 on their input schemas inherit this conformance.
- Stage 2 and later contexts add a catalogue under `Catalogues/`, a deviation list and a three-test conformance class, like Party. A rule that every context has them can be added once a second context exists.
- Any other quirk in a later BIAN file (another error-wired 2xx, another media-type key) shows up as a harness loader test failure and is handled here.

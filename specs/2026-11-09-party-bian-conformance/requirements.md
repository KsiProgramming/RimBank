# Party BIAN conformance

Roadmap phase: 1.14
Branch: plan/1.14-party-bian-conformance

## Context

Roadmap 1.14 asks for "OpenAPI and AsyncAPI conformance tests green for the implemented subset". Half of that already exists: since 0.14 ([ADR 0020](../../docs/adr/0020-bian-conformance-harness.md)) `PartyReferenceDataDirectoryConformanceTests` checks that the 13 Party routes are BIAN operations with the spec's method and that `Retrieve` is `GET`, and the catalogue tests check that the two published channel addresses exist in the AsyncAPI. What was explicitly left to 1.14:

- **Request and response bodies** (ADR 0020, "Out of scope"; the comment in `PartyReferenceDataDirectoryConformanceTests`: "The full subset check of the request and response shapes is roadmap 1.14").
- **Event payloads**: ADR 0036 says the camelCase wire names of `PartyReferenceDataDirectoryEntryCreated/Updated` are compared to BIAN's PascalCase names "without regard to case" in 1.14.
- The **declared operation list** in `RimBank.Api.Tests` is a hand-written array that can drift from the ADR 0024 catalogue in `RimBank.Conformance.Tests`.

Mission: the BIAN v14 ext-DDD YAML is the source of truth, and the quality bar requires conformance tests against it; "no speculative coverage" means RimBank implements a **subset** and never invents fields. Tech-stack §5 (BIAN routes and property names, Problem Details replaces `HTTPError`), §8.4 (published events carry the state consumers need) and §9 (contract row) drive the rules below.

Spec files: `doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml` and `.../asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml`.

Depends on 1.5–1.13 (all done). Unblocks 1.16/1.17 (MCP input schemas reuse the `.Api` models, so they inherit this conformance) and gives Stages 2–5 a ready-made harness.

## Scope

### In scope

- **Body schema subset** (harness, `RimBank.Conformance.Testing`): for each implemented operation, the JSON schema of its request model and of its success response model (exported with `JsonSchemaExporter` and the host's `JsonSerializerOptions`) is compared, recursively, to the BIAN schema of that operation's `requestBody` and `2xx` response:
  - every property RimBank serialises exists in the BIAN schema at the same JSON path (property names exact for HTTP bodies);
  - its JSON type is compatible (string/number/integer/boolean/object/array; `format` ignored);
  - BIAN enum wrappers (an object with a single string property, ADR 0024) match a plain JSON string;
  - fewer fields than BIAN is always fine (subset).
- **Success status codes**: each endpoint's declared 2xx (`IProducesResponseTypeMetadata`) is a 2xx the spec declares for that operation. Error statuses are not compared (RFC 9457 replaces BIAN's `HTTPError`, ADR 0015).
- **Event payload subset**: the JSON properties of each published integration event (Wolverine's camelCase wire format) exist in its AsyncAPI channel's message payload schema, compared **without regard to case** (ADR 0036). The AsyncAPI reader learns to read `components.messages.*.payload` and resolve `$ref`.
- **Deviations as data**: any field RimBank must carry but BIAN does not declare (for example the BQ ids in the Register response, ADR 0024) is listed in a per-context allow-list that the test reads, each row citing its ADR. A deviation not in the list fails.
- **Catalogue drives the declarations**: `PartyReferenceDataDirectoryCatalogue` moves from `RimBank.Conformance.Tests` to `RimBank.Conformance.Testing`; the Party conformance test declares its operations and channels as the catalogue's non-deferred rows, so the ADR 0024 table, the routing table and the published events cannot drift apart.
- Fix any real mismatch the new checks find in the Party `.Api` models or published events (rename, retype or drop), or record it as an ADR'd deviation.
- ADR 0037, and doc updates (tech-stack §9 contract row, CONTRIBUTING "BIAN conformance", ADR 0020 "Consequences" pointer).

### Out of scope

- An architecture rule that every context in `RimBank.Api` has a conformance test (left for when a second context exists, Stage 2).
- Error bodies and non-2xx statuses (Problem Details, ADR 0015).
- `required` / `enum` value / string-format checks: BIAN marks almost nothing required and RimBank's validation is a business rule in core, not a contract shape.
- Comparing against a generated OpenAPI document (Microsoft.AspNetCore.OpenApi is still not wired).
- Deferred operations (Control, Request, Execute, Exchange) and the 8 BQ channels (ADR 0024).
- Authorisation policies (1.15) and MCP tool lists (1.16, 1.17).

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Direction | RimBank ⊆ BIAN, per JSON path | Mission "no speculative coverage"; ADR 0020 already uses implemented ⊆ spec for routes. |
| How RimBank's shape is read | `JsonSchemaExporter` on the endpoint's request type (`IAcceptsMetadata`) and 2xx response type, using the host's `JsonOptions` | Uses the real serialiser contract (property names, `JsonElement?` passthroughs, ignore conditions), not reflection guesses; same exporter as the 1.13 schema snapshots (ADR 0036). |
| Type matching | JSON type only; `format`, `required`, `enum` ignored; enum wrapper ⇄ string accepted; `JsonElement` (accepted-and-ignored fields) matches any type | Keeps the check about shape, which is what BIAN conformance means here; the wrapper flattening is already decided in ADR 0024. |
| Name matching | Exact for HTTP bodies; case-insensitive for event payloads | HTTP models use BIAN PascalCase names (ADR 0024); events are camelCase on the wire (ADR 0036). |
| Status codes | Only 2xx compared | Errors are Problem Details (ADR 0015) and RimBank adds 409/428 by design (ADR 0018, 0027). |
| Deviations | Per-context allow-list (JSON path + ADR reference), read by the test | Visible, reviewable, and an unlisted deviation fails. |
| Catalogue location | `tests/RimBank.Conformance.Testing/Catalogues/` | Shared by `RimBank.Conformance.Tests` and `RimBank.Api.Tests`; a `*.Testing` project is not a production assembly, so its reference to Party `.PublishedLanguage` breaks no dependency rule. |
| Spec quirks | Tolerate the `'application/json:'` media-type key (trailing colon) in BIAN request bodies | `doc-0/` is read-only (ADR 0002); the harness reads the files as they are. |

## ADRs to write

- **ADR 0037 – BIAN body and payload conformance:** the subset rule per JSON path, how RimBank's schema is exported, type and name matching (enum wrappers, `JsonElement`, case-insensitive events), 2xx-only status checks, the deviation allow-list, the catalogue move, and the spec quirks tolerated. Updates ADR 0020's "Consequences" with a pointer.

## Open questions

- How many real mismatches exist today is unknown until the harness runs (group 3 of the plan). Each one is either fixed in the model or added to the allow-list with an ADR reference; if a fix would change an already-published contract shape, raise it in the PR before changing it.

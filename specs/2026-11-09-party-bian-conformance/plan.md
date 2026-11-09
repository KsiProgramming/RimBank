# Plan: Party BIAN conformance

Order: harness first (proved on the fake context), then the catalogue move, then Party wiring and fixes, then ADR and docs. The solution builds and all tests pass after each group.

## 1. Harness: read BIAN body and payload schemas
- [ ] `OpenApiSpecificationReader`: give `BianOperation` its request-body schema and its 2xx responses (status → schema), resolving `$ref` through Microsoft.OpenApi; accept the `'application/json:'` media-type key as JSON.
- [ ] `AsyncApiSpecificationReader`: give `BianChannel` its message payload schema (`channels.*.messages.*.$ref` → `components.messages.*.payload` → `components.schemas`), with YamlDotNet.
- [ ] A small neutral schema model (`BianSchemaNode`: JSON type, properties, items, `IsEnumWrapper`) both readers produce, so the comparison does not depend on Microsoft.OpenApi types.
- [ ] Loader tests in `RimBank.Conformance.Tests/Specifications/` on the real Party YAML: Register request has `PartyType`, Retrieve 200 has the CR fields, `PartyReferenceDataDirectoryEntry/Created` payload resolves.

## 2. Harness: subset assertions
- [ ] `RimBankSchema`: export a CLR type's JSON schema with `JsonSchemaExporter` and given `JsonSerializerOptions`, into the same node model (`JsonElement` → "any").
- [ ] `SchemaSubset.Compare(rimbank, bian, nameComparison)` → list of problems with JSON paths (`$.PartyReference.PartyName`): unknown property, incompatible type; enum wrapper ⇄ string accepted.
- [ ] `ImplementedRoute` carries the endpoint's request type (`IAcceptsMetadata`) and 2xx response types/statuses (`IProducesResponseTypeMetadata`).
- [ ] New assertions in `BianConformance`, all reported in one failure:
  - `routes.ShouldHaveBodiesConformingTo(specification, jsonOptions, deviations)`;
  - success statuses ⊆ spec 2xx (folded into the same call);
  - `publishedEvents.ShouldHavePayloadsConformingTo(specification, wireOptions, deviations)` (event type + its `Channel` const).
- [ ] `ConformanceDeviation(Operation or Channel, JsonPath, Adr)` record; a listed deviation that no longer occurs also fails (keeps the list honest).
- [ ] Self-tests on `FakeContexts/`: conforming fake passes; extra field, wrong type, nested extra field, undeclared 2xx, unknown event property, stale deviation each fail with a message naming the operation/channel and JSON path.

## 3. Catalogue drives the declarations
- [ ] Move `CataloguedOperation`, `CataloguedChannel` and `PartyReferenceDataDirectoryCatalogue` to `tests/RimBank.Conformance.Testing/Catalogues/` (namespace `RimBank.Conformance.Testing`); reference Party `.PublishedLanguage` from it; keep `PartyReferenceDataDirectoryCatalogueTests` in `RimBank.Conformance.Tests`.
- [ ] Add `Implemented` (non-deferred operation ids) and `Published` (non-deferred channels with their event types) helpers.
- [ ] Architecture tests still green (the `*.Testing` project is not production code).

## 4. Party: wire the checks and fix mismatches
- [ ] `PartyReferenceDataDirectoryConformanceTests`: replace the hand-written `_implemented` array with `PartyReferenceDataDirectoryCatalogue.Implemented`; add body/status and event payload tests against the running host (Testcontainers), using the host's `JsonOptions` and Wolverine's serializer options.
- [ ] `PartyReferenceDataDirectoryConformanceDeviations` (in `RimBank.Api.Tests`): start empty, run the tests, and triage each problem:
  - fix in the `.Api` model or the published event when RimBank simply mis-named or mis-typed a BIAN field;
  - otherwise add a deviation row citing the ADR that decided it (for example ADR 0024 for the BQ ids in the Register response).
- [ ] Ensure every Party endpoint exposes its request type and 2xx response in metadata (add `.Accepts<T>()` / `.Produces<T>()` where missing).
- [ ] Update the endpoint and published-event tests and the 1.13 schema snapshots if a fix changes a shape (flag any published-contract change in the PR).
- [ ] Remove the "full subset check … is roadmap 1.14" comment.

## 5. ADR, docs and roadmap
- [ ] `docs/adr/0037-bian-body-and-payload-conformance.md` (from `template.md`); pointer added to ADR 0020 "Consequences" and ADR 0036 "wire format".
- [ ] tech-stack §9 contract row and §3.3 (`RimBank.Conformance.Testing` now holds the catalogues); CONTRIBUTING "BIAN conformance" (declare via catalogue, deviations list).
- [ ] Tick **1.14** in `specs/roadmap.md`.
- [ ] `dotnet format --verify-no-changes`, full test run.

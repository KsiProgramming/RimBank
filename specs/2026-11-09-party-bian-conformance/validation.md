# Validation: Party BIAN conformance

## Done when (from roadmap)
> **1.14 Conformance:** OpenAPI and AsyncAPI conformance tests green for the implemented subset.
> + Definition of done (roadmap.md)

Read as: for every non-deferred operation and channel of ADR 0024, the route, method, success status, request body, response body and event payload are a subset of the BIAN v14 ext-DDD YAML, and every exception is listed with its ADR.

## Automated checks
- [ ] `RimBank.Conformance.Tests/Specifications`: the Party OpenAPI request/2xx schemas and AsyncAPI payloads load and resolve `$ref` (including the `'application/json:'` key).
- [ ] `RimBank.Conformance.Tests/FakeContexts`: the conforming fake passes; each of these fails with a message naming the operation or channel and the JSON path:
  - [ ] extra top-level request property;
  - [ ] extra nested response property;
  - [ ] incompatible JSON type;
  - [ ] success status the spec does not declare;
  - [ ] unknown event payload property (and a case-only difference passes);
  - [ ] a deviation listed but no longer occurring.
- [ ] `RimBank.Conformance.Tests/Catalogues`: catalogue tests still green after the move.
- [ ] `RimBank.Api.Tests` `PartyReferenceDataDirectoryConformanceTests` (Testcontainers host): routes, bodies, statuses and both published channels green; declarations come from the catalogue, not a hand-written list.
- [ ] Every row of `PartyReferenceDataDirectoryConformanceDeviations` cites an ADR.
- [ ] Existing Party endpoint, MCP and published-language tests (including the 1.13 schema snapshots) green, updated only where a fix changed a shape.
- [ ] `RimBank.Architecture.Tests` green.
- [ ] `dotnet format --verify-no-changes` clean; CI (build, test, format, CodeQL) green.

## Manual / demo checks
- [ ] `dotnet test tests/RimBank.Conformance.Tests` and `dotnet test tests/RimBank.Api.Tests --filter FullyQualifiedName~Conformance` pass locally.
- [ ] Read one failure message from the fake-context self-tests in the test output: it names the BIAN operation, the JSON path and the rule broken, and says how to fix it or which ADR to write.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.14 ticked in the same PR
- [ ] ADR 0037 written; ADR 0020 and ADR 0036 point to it
- [ ] Any change to a published HTTP or event shape is called out in the PR description
- [ ] tech-stack §3.3/§9 and CONTRIBUTING updated
- [ ] No new production dependency (harness changes stay in `tests/`), no secrets or PII in test data or logs

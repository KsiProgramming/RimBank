# Validation: BIAN conformance harness

## Done when (from roadmap)

> **0.14 BIAN conformance harness:** a test utility that loads an ext-DDD OpenAPI/AsyncAPI YAML from `doc-0/release14.0.0/` and asserts that the implemented routes and channels exist, and that `Retrieve` operations are `GET` (queries) and the rest are commands.
> + Definition of done (roadmap.md)

The Definition of done items about read models, projections, Problem Details on new error paths and MCP parity do not apply: this phase adds no domain code, endpoint or tool.

## Automated checks

**Loader tests on the real YAML** (`RimBank.Conformance.Tests`, folder `Specifications/`)
- [x] The Party OpenAPI spec loads with no reader errors.
- [x] `PartyReferenceDataDirectoryEntry/Register` → `POST /PartyReferenceDataDirectory/Register`, command.
- [x] `PartyReferenceDataDirectoryEntry/Retrieve` → `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve`, query.
- [x] `PartyReferenceDataDirectoryEntry/Update` → `PUT`, command; `Reference/Retrieve` and `Associations/Retrieve` are queries.
- [x] In the spec, every `Retrieve` is `GET` and nothing else is `GET`.
- [x] The Party AsyncAPI spec has exactly the 10 expected channel addresses.
- [x] An unknown service domain fails with a message that lists the available specs.

**Routing unit tests** (folder `Routing/`)
- [x] `{id}` matches `{partyreferencedatadirectoryid}`; constraints are ignored; case differences match; literal mismatches do not.

**Fake-context self-tests** (folder `FakeContexts/`)
- [x] A conforming subset (Register `POST`, Retrieve `GET`, Update `PUT`) passes.
- [x] Each of these fails, with a message that names the BIAN operation, route or channel:
  - [x] a declared operation that is not mapped
  - [x] a wrong method (`POST …/{id}/Update`)
  - [x] `Retrieve` mapped as `POST` (command/query rule)
  - [x] a command mapped as `GET` (command/query rule)
  - [x] a route that is not in the spec
  - [x] a channel that is not in the AsyncAPI spec
- [x] Routes outside `/<ServiceDomain>/` (for example `/health`) are ignored.
- [x] Several violations are reported in one message.

**Repository checks**
- [x] `RimBank.Architecture.Tests` green with both new projects referenced (namespace and `.Tests` rules).
- [x] `dotnet format --verify-no-changes` clean.
- [x] Locked restore succeeds (`packages.lock.json` committed for both new projects).
- [ ] CI workflow (`ci.yml`) and CodeQL green.

## Manual / demo checks

- [x] `dotnet test tests/RimBank.Conformance.Tests`: all tests pass.
- [x] Temporarily change a self-test route to `GET /PartyReferenceDataDirectory/{id}/Update` and run the tests again. The failure message names `PartyReferenceDataDirectoryEntry/Update`, expects `PUT`, and cites the command/query rule. Then revert the change.
- [x] Read the CONTRIBUTING "BIAN conformance" section: a reader can write the 1.14 conformance test from it alone.

## Merge criteria

- [ ] All checks above pass in CI
- [x] Roadmap checkbox 0.14 ticked in the same PR
- [x] ADR 0020 written (packages, matching rules, command/query rule, spec location, out-of-scope items)
- [x] New package versions only in `Directory.Packages.props`, with no `VersionOverride`
- [x] `specs/tech-stack.md` §3.3 and §9 and `CONTRIBUTING.md` updated
- [x] Nothing under `doc-0/` modified (ADR 0002)

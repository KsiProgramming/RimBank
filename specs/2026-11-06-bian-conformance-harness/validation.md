# Validation: BIAN conformance harness

## Done when (from roadmap)

> **0.14 BIAN conformance harness:** a test utility that loads an ext-DDD OpenAPI/AsyncAPI YAML from `doc-0/release14.0.0/` and asserts that the implemented routes and channels exist, and that `Retrieve` operations are `GET` (queries) and the rest are commands.
> + Definition of done (roadmap.md)

The Definition of done items about read models, projections, Problem Details on new error paths and MCP parity do not apply: this phase adds no domain code, endpoint or tool.

## Automated checks

**Loader tests on the real YAML** (`RimBank.Conformance.Tests`, folder `Specifications/`)
- [ ] The Party OpenAPI spec loads with no reader errors.
- [ ] `PartyReferenceDataDirectoryEntry/Register` → `POST /PartyReferenceDataDirectory/Register`, command.
- [ ] `PartyReferenceDataDirectoryEntry/Retrieve` → `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve`, query.
- [ ] `PartyReferenceDataDirectoryEntry/Update` → `PUT`, command; `Reference/Retrieve` and `Associations/Retrieve` are queries.
- [ ] In the spec, every `Retrieve` is `GET` and nothing else is `GET`.
- [ ] The Party AsyncAPI spec has exactly the 10 expected channel addresses.
- [ ] An unknown service domain fails with a message that lists the available specs.

**Routing unit tests** (folder `Routing/`)
- [ ] `{id}` matches `{partyreferencedatadirectoryid}`; constraints are ignored; case differences match; literal mismatches do not.

**Fake-context self-tests** (folder `FakeContexts/`)
- [ ] A conforming subset (Register `POST`, Retrieve `GET`, Update `PUT`) passes.
- [ ] Each of these fails, with a message that names the BIAN operation, route or channel:
  - [ ] a declared operation that is not mapped
  - [ ] a wrong method (`POST …/{id}/Update`)
  - [ ] `Retrieve` mapped as `POST` (command/query rule)
  - [ ] a command mapped as `GET` (command/query rule)
  - [ ] a route that is not in the spec
  - [ ] a channel that is not in the AsyncAPI spec
- [ ] Routes outside `/<ServiceDomain>/` (for example `/health`) are ignored.
- [ ] Several violations are reported in one message.

**Repository checks**
- [ ] `RimBank.Architecture.Tests` green with both new projects referenced (namespace and `.Tests` rules).
- [ ] `dotnet format --verify-no-changes` clean.
- [ ] Locked restore succeeds (`packages.lock.json` committed for both new projects).
- [ ] CI workflow (`ci.yml`) and CodeQL green.

## Manual / demo checks

- [ ] `dotnet test tests/RimBank.Conformance.Tests`: all tests pass.
- [ ] Temporarily change a self-test route to `GET /PartyReferenceDataDirectory/{id}/Update` and run the tests again. The failure message names `PartyReferenceDataDirectoryEntry/Update`, expects `PUT`, and cites the command/query rule. Then revert the change.
- [ ] Read the CONTRIBUTING "BIAN conformance" section: a reader can write the 1.14 conformance test from it alone.

## Merge criteria

- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 0.14 ticked in the same PR
- [ ] ADR 0020 written (packages, matching rules, command/query rule, spec location, out-of-scope items)
- [ ] New package versions only in `Directory.Packages.props`, with no `VersionOverride`
- [ ] `specs/tech-stack.md` §3.3 and §9 and `CONTRIBUTING.md` updated
- [ ] Nothing under `doc-0/` modified (ADR 0002)

# BIAN conformance harness

Roadmap phase: 0.14
Branch: plan/0.14-bian-conformance-harness

## Context

The BIAN v14 ext-DDD specifications are RimBank's source of truth for routes, operations and integration events (mission, "Source of truth"; [ADR 0002](../../docs/adr/0002-bian-specs-location.md)). The quality bar asks for "conformance tests against the BIAN specs", and the contract row of [tech-stack §9](../tech-stack.md#9-testing-stack) says implemented routes and events must match the YAML. The HTTP rule in [tech-stack §5](../tech-stack.md#5-api--error-contract) and mission principle 5 add that `Retrieve` operations are queries (`GET`) and every other operation is a command.

This phase builds the harness that checks those rules. No context has endpoints or published events yet, so the harness is proved on a test-only fake context, as 0.10 did with its fake contexts. It depends only on what Stage 0 already ships (the endpoint routing of ASP.NET Core and the test platform from [ADR 0007](../../docs/adr/0007-test-platform.md)). It unblocks **1.14 Conformance** and the per-context `.Api.Tests` conformance tests (tech-stack §3.3). Those tests then only declare which BIAN operations and channels they implement and call the harness.

Spec files in scope: `doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml` (OpenAPI 3.0.1) and `.../asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml` (AsyncAPI 3.0.0), which are the only service domains in the repository today.

## Scope

### In scope

- A new shared test library, `tests/RimBank.Conformance.Testing`, holding:
  - **Spec location:** finds `doc-0/release14.0.0/apis-iso20022_ext-ddd/` by walking up from the test output to the folder that holds `RimBank.slnx`. It loads a service domain by its BIAN name (for example `PartyReferenceDataDirectory`), and an unknown name fails with a message that lists the available specs.
  - **A small BIAN spec model:** `BianOperation` (operation id such as `PartyReferenceDataDirectoryEntry/Register`, HTTP method, path template, and kind `Query` when the action is `Retrieve`, otherwise `Command`) and `BianChannel` (address such as `PartyReferenceDataDirectoryEntry/Created`).
  - **OpenAPI loader** built on Microsoft.OpenApi and its YAML reader. **AsyncAPI loader** built on YamlDotNet, reading only `channels.*.address`.
  - **Implemented routes** read from an ASP.NET Core `EndpointDataSource` (method and route pattern). Templates are compared after normalisation: matching ignores case, every `{parameter}` matches any parameter name (so `{id}` matches `{partyreferencedatadirectoryid}`), and route constraints are dropped.
  - **Conformance assertions** (Shouldly, with messages that name the BIAN operation or channel):
    1. Every implemented route under `/<ServiceDomain>/` matches a spec operation with **the same method as the spec** (for example `PUT` for `Update`, `POST` for `Register`).
    2. Every operation the context **declares** as implemented is mapped, so a dropped route fails.
    3. **Command/query rule:** a `Retrieve` operation is mapped as `GET`, and no non-`Retrieve` operation is mapped as `GET`. If the spec itself breaks this rule, the harness fails and the case needs an ADR.
    4. Every channel the context declares as published exists in the AsyncAPI spec.
- A new test project, `tests/RimBank.Conformance.Tests`, with loader tests on the real Party YAML and self-tests on a fake context.
- ADR 0020, new package versions in `Directory.Packages.props`, and doc updates (tech-stack §3.3 and §9, CONTRIBUTING).

### Out of scope

- Request and response **schema** conformance (ISO 20022 body shapes) and error bodies. RFC 9457 replaces BIAN's `HTTPError` ([ADR 0015](../../docs/adr/0015-problem-details-mapping.md)). Schema checks can be added per context in **1.14** if useful.
- Checking that an endpoint dispatches an `ICommand` or `IQuery<TView>`. The architecture tests already enforce the CQRS roles ([ADR 0012](../../docs/adr/0012-cqrs-role-markers.md)). The harness checks HTTP methods only.
- How a `.PublishedLanguage` integration event declares its channel address (attribute, constant, or Wolverine routing). The harness takes a set of addresses, and **1.13** decides how events declare their channel.
- An architecture rule that every `.Api.Tests` project contains a conformance test. This is deferred to **1.14**, when the first context exists.
- Comparing against the host's generated OpenAPI document (Microsoft.AspNetCore.OpenApi). Not wired yet.
- MCP tool-list conformance. This is covered in **1.17**.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Where the harness lives | `tests/RimBank.Conformance.Testing` (shared test library, `*.Testing` like `RimBank.SharedKernel.Testing`) plus `tests/RimBank.Conformance.Tests` | A `*.Testing` library is not a production assembly (`CodeBases.IsProductionAssembly`). Each context's `.Api.Tests` references it (tech-stack §3.3). |
| OpenAPI parsing | **Microsoft.OpenApi** + **Microsoft.OpenApi.YamlReader** (v2 line, same major as Microsoft.AspNetCore.OpenApi on .NET 10) | Official, maintained, handles OAS 3.0 and `$ref`. One major version avoids a conflict once the host generates OpenAPI. |
| AsyncAPI parsing | **YamlDotNet**, reading a minimal subset into the spec model | There is no mature .NET AsyncAPI 3 parser. The harness needs only channel addresses and operation actions. |
| Source of implemented routes | `EndpointDataSource` of a running test app (in-memory, no requests sent) | Takes the real routing table, not a hand-written list, and needs no generated OpenAPI. |
| Path parameter names | Ignored when matching; literal segments compared without regard to case | BIAN names parameters `{partyreferencedatadirectoryid}`, while RimBank routes use `{id}` (roadmap 1.7, tech-stack §5). Parameter names are not part of the HTTP contract. |
| Method rule | The method must **equal** the spec method | BIAN uses `PUT` for most commands and `POST` for `Register`. Tech-stack §5 allows `POST`/`PUT` for commands, and the spec decides which one. |
| Direction of the check | Implemented ⊆ spec, and declared ⊆ implemented | "Implemented routes and channels exist" (roadmap 0.14). Contexts implement a subset (mission, "no speculative coverage"), so the check never requires all spec operations. |
| Spec diagnostics | OpenAPI reader **errors** fail the load; warnings are ignored | The Party YAML must load as it is (`doc-0/` is read-only, ADR 0002). A loader test proves it. |
| How the self-tests prove the harness | A fake context in `RimBank.Conformance.Tests/FakeContexts/` maps a subset of the Party routes on a slim `WebApplication` | Same pattern as 0.10's fakes, and it uses the real spec, so no invented YAML is needed. |

## ADRs to write

- **ADR 0020 – BIAN conformance harness:** the packages (Microsoft.OpenApi, Microsoft.OpenApi.YamlReader, YamlDotNet), the matching rules (parameter-name normalisation, exact method, implemented ⊆ spec, declared ⊆ implemented), the command/query rule and what happens when the spec breaks it, how the spec is located, and what is out of scope (schemas, error bodies, and the channel declaration left to 1.13).

## Open questions

- None. The channel declaration mechanism is deliberately left to 1.13 (see Out of scope).

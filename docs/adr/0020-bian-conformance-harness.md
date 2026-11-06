# 0020. BIAN conformance harness

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.14

## Context

The BIAN v14 ext-DDD specifications in `doc-0/release14.0.0/apis-iso20022_ext-ddd/` ([ADR 0002](0002-bian-specs-location.md)) are RimBank's source of truth for routes, operations and integration event channels (mission, "Source of truth"). The quality bar asks for conformance tests against them, and the contract row of [tech-stack §9](../../specs/tech-stack.md#9-testing-stack) says implemented routes and events must match the YAML. [Tech-stack §5](../../specs/tech-stack.md#5-api--error-contract) and mission principle 5 add the command/query rule: `Retrieve` operations are `GET` queries, and every other operation is a command.

Roadmap 0.14 asks for a test utility that loads a service domain's OpenAPI and AsyncAPI YAML and asserts these rules. Each context's `.Api.Tests` uses it, first in 1.14. A context implements a subset of its spec (mission non-goal "no speculative coverage"), and grows it one phase at a time.

These are the facts the harness must live with:

- The OpenAPI files are OpenAPI 3.0.1. BIAN names path parameters after the record (`{partyreferencedatadirectoryid}`), while RimBank routes use `{id}` (roadmap 1.7).
- BIAN uses `POST` for `Register` and `PUT` for the other commands (`Update`, `Control`, `Exchange`, …). Tech-stack §5 allows `POST` or `PUT` for a command.
- The AsyncAPI files are AsyncAPI 3.0.0. A channel has an `address` such as `PartyReferenceDataDirectoryEntry/Created`.
- `doc-0/` is read-only (ADR 0002), so the harness must read the files as they are.

## Options considered

How the specifications are read:

1. **YamlDotNet only, with a hand-written reader for both formats.** One package, but it re-implements OpenAPI parsing (`$ref`, path items, operation maps).
2. **Microsoft.OpenApi with its YAML reader for OpenAPI, and YamlDotNet for the AsyncAPI subset.** The official OpenAPI object model reads OpenAPI 3.0 and reports diagnostics. There is no mature .NET parser for AsyncAPI 3, and the harness needs only the channel addresses, so a small YamlDotNet reader is enough.
3. **Compare against the host's generated OpenAPI document** (Microsoft.AspNetCore.OpenApi) instead of its routing table. This also proves the published contract, but it pulls OpenAPI generation forward, and the BIAN YAML still has to be read.

Where the implemented routes come from:

1. **A hand-written list in each test.** Simple, but it can drift from what the application really maps.
2. **The application's routing table** (`EndpointDataSource`, or an `IEndpointRouteBuilder` before start-up), read without sending a request.

## Decision

- **Packages:** Microsoft.OpenApi and Microsoft.OpenApi.YamlReader read the OpenAPI files, and YamlDotNet reads `channels.*.address` from the AsyncAPI files (option 2). Microsoft.OpenApi stays on the **2.x** line, because Microsoft.AspNetCore.OpenApi for .NET 10 requires `[2.12.2, 3.0.0)`, and the host will later generate its own document with the same object model. Dependabot ignores Microsoft.OpenApi major versions. YamlDotNet already came in transitively through Aspire; it is now pinned centrally.
- **Where it lives:** `tests/RimBank.Conformance.Testing`, a shared test library (a `*.Testing` project, so it is not a production assembly). Its own tests are in `tests/RimBank.Conformance.Tests`. They read the real Party YAML and use a test-only fake context that maps routes on a slim `WebApplication`.
- **Spec location:** the harness walks up from the test output to the folder that holds `RimBank.slnx`, then reads `doc-0/release14.0.0/apis-iso20022_ext-ddd/{oas3,asyncapi-3.x}/yamls/<ServiceDomain>.yaml`. An unknown service domain fails with the list of available ones. OpenAPI reader **errors** fail the load, and warnings are ignored. Each service domain is read once per test run.
- **Routes come from the routing table** (`ImplementedRoute.From(EndpointDataSource)` or `From(IEndpointRouteBuilder)`), not from a hand-written list.
- **Matching:** literal segments are compared without regard to case. Every `{parameter}` matches any other, whatever its name, constraint or optional marker. Leading and trailing `/` do not matter.
- **Rules** (`routes.ShouldConformTo(specification, declaredOperationIds)`), all reported in one failure:
  1. The specification follows the command/query rule: only `Retrieve` is `GET`. If BIAN ever breaks it, the test fails, and adopting that operation needs its own ADR.
  2. Every route whose first segment is the service domain is an operation of the specification, **with the same method as the specification**. Other routes (health probes, `/mcp`, other contexts) are ignored.
  3. A `Retrieve` operation mapped with another method, or a command mapped as `GET`, is reported as a command/query violation.
  4. Every operation the context declares as implemented is mapped. In short: implemented ⊆ spec, and declared ⊆ implemented. The harness never requires all spec operations.
- **Channels** (`publishedAddresses.ShouldBeChannelsOf(specification)`): every address a context publishes to exists in its AsyncAPI specification, compared exactly. How a `.PublishedLanguage` event declares its address (an attribute, a constant, or Wolverine routing) is decided in roadmap 1.13. The harness only takes the addresses.
- **Out of scope:** request and response schemas (ISO 20022 bodies), error bodies (RFC 9457 replaces BIAN's `HTTPError`, [ADR 0015](0015-problem-details-mapping.md)), whether an endpoint dispatches an `ICommand` or `IQuery<TView>` (enforced by the architecture tests, [ADR 0012](0012-cqrs-role-markers.md)), and MCP tool lists (roadmap 1.17).

## Consequences

- Each context's `.Api.Tests` references `RimBank.Conformance.Testing` and adds one conformance test (CONTRIBUTING, "BIAN conformance"). Roadmap 1.14 is the first such test, and a rule that every `.Api.Tests` has one can be added then.
- A route that is not in the BIAN spec cannot be added to a context without failing its conformance test. A RimBank-specific route needs an ADR and has to live outside `/<ServiceDomain>/`.
- Choosing `POST` or `PUT` for a command is not a free choice: the spec decides.
- A new BIAN release (a new `doc-0/release…/` folder, with its own ADR) changes `BianSpecifications.RelativePath`.
- Moving Microsoft.OpenApi to 3.x waits until Microsoft.AspNetCore.OpenApi supports it.

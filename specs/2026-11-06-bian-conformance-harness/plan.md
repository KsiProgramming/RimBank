# Plan: BIAN conformance harness

Order: ADR → spec model and loaders, tested on the real Party YAML → route and channel matching → command/query rule → fake-context self-tests → docs. The solution builds and every test passes after each group.

## 1. ADR and packages

- [ ] Write `docs/adr/0020-bian-conformance-harness.md` from `docs/adr/template.md` (content listed in requirements.md, "ADRs to write").
- [ ] Add a `Conformance` item group to `Directory.Packages.props` with `Microsoft.OpenApi`, `Microsoft.OpenApi.YamlReader` (sharing one version property, on the v2 line that matches Microsoft.AspNetCore.OpenApi for .NET 10) and `YamlDotNet`, with a comment that cites ADR 0020.
- [ ] Update `specs/tech-stack.md`: §3.3 layout (`RimBank.Conformance.Testing/`, `RimBank.Conformance.Tests/`) and the §9 contract row (link ADR 0020).

## 2. Projects and spec location

- [ ] Create `tests/RimBank.Conformance.Testing/RimBank.Conformance.Testing.csproj` (library; packages from group 1, `Shouldly`, `FrameworkReference Microsoft.AspNetCore.App`), and add it to `RimBank.slnx`.
- [ ] Create `tests/RimBank.Conformance.Tests/RimBank.Conformance.Tests.csproj` (xUnit v3 exe, Shouldly, a reference to `.Testing`), and add it to `RimBank.slnx`.
- [ ] Add both as `ProjectReference`s of `RimBank.Architecture.Tests.csproj` so the naming and namespace rules cover them (CONTRIBUTING, "Architecture rules").
- [ ] `BianSpecifications`: walk up from `AppContext.BaseDirectory` to the folder that holds `RimBank.slnx`, then resolve `doc-0/release14.0.0/apis-iso20022_ext-ddd/{oas3,asyncapi-3.x}/yamls/<ServiceDomain>.yaml`. An unknown service domain fails with the list of available names.
- [ ] Tests (`RimBank.Conformance.Tests`, namespace `RimBank.Conformance.Tests`): resolves the Party specs, and an unknown name fails with a clear message.
- [ ] Generate `packages.lock.json` for both projects (`dotnet restore --force-evaluate`).

## 3. Spec model and loaders

- [ ] `BianOperation` (`OperationId`, `Action` (last segment of the operation id), `Method`, `PathTemplate`, `Kind`), `BianOperationKind { Command, Query }`, `BianChannel` (`Address`, `Action`), and `BianServiceDomainSpecification` (`ServiceDomain`, `Operations`, `Channels`). All are immutable records.
- [ ] OpenAPI loader: `OpenApiYamlReader` / `OpenApiDocument.LoadAsync` with `settings.AddYamlReader()`. It maps every path and method to a `BianOperation`, and reader errors fail with their diagnostics listed.
- [ ] AsyncAPI loader: YamlDotNet reads `channels.*.address` and `operations.*.action`. Unknown fields are ignored.
- [ ] Loader tests on the real Party YAML (folder `Specifications/`, namespace `RimBank.Conformance.Specifications.Tests`):
  - `PartyReferenceDataDirectoryEntry/Register` is `POST /PartyReferenceDataDirectory/Register`, a command.
  - `PartyReferenceDataDirectoryEntry/Retrieve` is `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve`, a query.
  - `PartyReferenceDataDirectoryEntry/Update` is `PUT`, a command; `Reference/Retrieve` and `Associations/Retrieve` are BQ queries.
  - Every `Retrieve` in the spec is `GET`, and no other operation is `GET` (the spec meets the rule).
  - The AsyncAPI file has the 10 channels (`PartyReferenceDataDirectoryEntry/Created`, `…/Updated`, and `Demographics`, `Associations`, `BankRelations` and `Reference` `Created`/`Updated`).

## 4. Implemented routes and matching

- [ ] `ImplementedRoutes.From(EndpointDataSource)`: collects the `RouteEndpoint`s with `HttpMethodMetadata`, as (method, route pattern).
- [ ] `RouteTemplate.Normalise`: drops a leading `/`, matches literal segments without regard to case, turns every `{name[:constraint][?]}` into a parameter placeholder, and keeps the raw text for messages.
- [ ] Unit tests (folder `Routing/`): `{id}` matches `{partyreferencedatadirectoryid}`, a constraint is ignored, a literal mismatch does not match, and case differences match.

## 5. Conformance assertions and the command/query rule

- [ ] `BianConformance.ShouldConformTo(spec, implementedRoutes, declaredOperationIds)`:
  1. Every implemented route whose first segment is the service domain matches a spec operation with the same method. A failure names the route, its method and the nearest spec operation.
  2. Every declared operation id exists in the spec and is mapped.
  3. A `Retrieve` operation must be `GET`, and `GET` is only allowed for `Retrieve`. The spec is checked first: a spec that breaks the rule fails with "needs an ADR".
  - All failures are collected into one assertion message, not stopped at the first one.
- [ ] `BianConformance.ShouldPublishOnlySpecChannels(spec, publishedAddresses)`: every address exists in the AsyncAPI spec.
- [ ] XML docs on every public type (the API used by 1.14 and the `.Api.Tests` projects).

## 6. Fake-context self-tests

- [ ] `tests/RimBank.Conformance.Tests/FakeContexts/`: a helper that builds a slim `WebApplication` (no server started), maps a given set of (method, route) pairs with no-op delegates, and returns its `EndpointDataSource`.
- [ ] Self-tests (namespace `RimBank.Conformance.FakeContexts.Tests`) against the real Party spec:
  - Conforming subset passes: `POST /PartyReferenceDataDirectory/Register`, `GET /PartyReferenceDataDirectory/{id}/Retrieve`, `PUT /PartyReferenceDataDirectory/{id}/Update`.
  - A declared operation that is not mapped fails and names it.
  - A wrong method (`POST …/{id}/Update`) fails.
  - `Retrieve` mapped as `POST` fails with the command/query message.
  - A command mapped as `GET` (`GET …/{id}/Update`) fails with the command/query message.
  - A route that is not in the spec (`POST /PartyReferenceDataDirectory/{id}/Archive`) fails.
  - A route outside the service domain prefix (for example `/health`) is ignored.
  - Channels: `PartyReferenceDataDirectoryEntry/Created` passes; `PartyReferenceDataDirectoryEntry/Deleted` fails.
  - Several violations are reported together in one message.

## 7. Docs and wrap-up

- [ ] `CONTRIBUTING.md`: a short "BIAN conformance" section on how a context's `.Api.Tests` declares its implemented operations and channels and calls the harness.
- [ ] Tick **0.14** in `specs/roadmap.md`.
- [ ] `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` green locally.

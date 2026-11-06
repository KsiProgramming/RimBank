# Plan: Idempotency

The solution builds and every test passes after each group.

## 1. ADR and constitution
- [ ] `docs/adr/0018-idempotency-keys-for-commands.md` (from `template.md`): the options considered (HTTP response cache, a shared host table, a per-context bus policy) and the decisions in [requirements.md](requirements.md)
- [ ] `specs/tech-stack.md`: §3.3 adds `RimBank.Http`; §4 adds a `RimBank.Http` row and notes the idempotency policy in the `RimBank.Postgresql` row; §5 and §8.1 link to ADR 0018
- [ ] `CONTRIBUTING.md`: an "Idempotency" section (every command carries a key; the HTTP header binder; the error codes)

## 2. SharedKernel: the key on every command
- [ ] `src/RimBank.SharedKernel/IdempotencyKey.cs`: a `readonly record struct` with `TryCreate(string?) → Result<IdempotencyKey>` (1–255 printable ASCII characters) and `ToString()`
- [ ] `ICommand`: add `IdempotencyKey IdempotencyKey { get; }`, and update the XML doc (ADR 0012, ADR 0018)
- [ ] Update every existing `ICommand` (the fake contexts in `RimBank.Postgresql.Tests`, `RimBank.Host.Tests` and `RimBank.Architecture.Fixtures`) with an `IdempotencyKey` parameter
- [ ] `tests/RimBank.SharedKernel.Tests/Idempotency/IdempotencyKeyTests.cs` (namespace `RimBank.SharedKernel.Idempotency.Tests`): empty, whitespace, too long and non-ASCII keys are rejected; a valid key round-trips

## 3. Per-context idempotency policy (RimBank.Postgresql)
- [ ] `IdempotentOutcome` document (id `"<command type>:<key>"`, `Fingerprint`, `Acknowledgement` JSON, `RecordedAt` from `TimeProvider`), registered in each context's store schema by `AddServiceDomainStore<TStore>`
- [ ] `CommandFingerprint`: SHA-256 of the command's System.Text.Json serialization with `IdempotencyKey` excluded
- [ ] `IdempotencyMiddleware`, applied by a chain policy only to chains whose message implements `ICommand` and that are bound to the context's store. It handles lookup, replay, `shared.idempotency-key-reused`, and storing the outcome on success in the handler's session
- [ ] In-flight concurrency: settle the open question (unique insert vs advisory lock) and map the loser to `shared.idempotency-key-in-use`
- [ ] `IdempotencyErrors` in SharedKernel (the `Error.Conflict` codes) or `RimBank.Problems` (the documented types), following ADR 0015 for `shared.*` codes
- [ ] `tests/RimBank.Postgresql.Tests/Idempotency/IdempotencyTests.cs` (Testcontainers, see validation.md)

## 4. HTTP binder (RimBank.Http)
- [ ] New project `src/RimBank.Http/RimBank.Http.csproj` (SharedKernel, `RimBank.Problems`, ASP.NET Core framework reference), added to `RimBank.slnx`, with a lock file
- [ ] `IdempotencyKeyHeader`: reads `Idempotency-Key` for an endpoint (a `BindAsync` parameter type or an endpoint filter, `RequireIdempotencyKey()`). A missing header gives `shared.idempotency-key-missing` and an invalid one `shared.idempotency-key-invalid`, both 400 Problem Details
- [ ] `RimBank.Problems`: map `shared.idempotency-key-reused` and `shared.idempotency-key-in-use` to 409 with stable `urn:rimbank:problem:shared:…` types
- [ ] `tests/RimBank.Http.Tests/`: in-process `TestServer` with a test-only `POST` endpoint (missing header → 400 `application/problem+json`; a valid header reaches the handler)

## 5. Architecture rules and roadmap
- [ ] `RimBank.Architecture.Tests`: a dependency rule for `RimBank.Http` (only `.Api` and `RimBank.Host` may reference it; it references no core, `.Postgresql` or Marten/Wolverine), with a bad fixture
- [ ] Tick 0.12 in `specs/roadmap.md`; tick the completed plan and validation items; record the answered open questions in requirements.md
- [ ] `dotnet format --verify-no-changes` and the full test run are green locally

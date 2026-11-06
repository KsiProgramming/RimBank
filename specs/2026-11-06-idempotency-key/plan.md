# Plan: Idempotency

The solution builds and every test passes after each group.

## 1. ADR and constitution
- [x] `docs/adr/0018-idempotency-keys-for-commands.md` (from `template.md`): the options considered (HTTP response cache, a check in each handler, a per-context bus policy; unique insert vs advisory lock; the header's home) and the decisions in [requirements.md](requirements.md)
- [x] `specs/tech-stack.md`: §3.3 adds `RimBank.Http` and `RimBank.Http.Tests`; §4 adds a `RimBank.Http` row, allows it in the `.Api` row and notes the idempotency policy in the `RimBank.Postgresql` row; §5 and §8.1 link to ADR 0018
- [x] `CONTRIBUTING.md`: "Idempotency" and "HTTP" bullets under "Messaging" (every command carries a key; the HTTP header binder; the error codes) and the two new rules in the catalogue
- [x] ADR 0017's deferred list links to ADR 0018

## 2. SharedKernel: the key on every command
- [x] `src/RimBank.SharedKernel/IdempotencyKey.cs`: a `readonly record struct` with `TryCreate(string?) → Result<IdempotencyKey>` (1–255 visible ASCII characters; `null` gives `missing`), `From` for trusted input, and `ToString()`
- [x] `IdempotencyKeyJsonConverter`: a plain JSON string
- [x] The four `shared.idempotency-*` errors live on `IdempotencyKey` (`Missing`, `Invalid`, `Reused`, `InUse`), so `RimBank.Postgresql` and `RimBank.Http` share one definition
- [x] `ICommand`: add `IdempotencyKey IdempotencyKey { get; }`, and update the XML doc (ADR 0012, ADR 0018)
- [x] Update every existing `ICommand` (the fake contexts in `RimBank.Postgresql.Tests` and `RimBank.Architecture.Fixtures`; `RimBank.Host.Tests` had none) with an `IdempotencyKey` parameter; the fixture `.Subscriptions` builds its key with `IdempotencyKey.From(id)`
- [x] `tests/RimBank.SharedKernel.Tests/Idempotency/IdempotencyKeyTests.cs` (namespace `RimBank.SharedKernel.Idempotency.Tests`): empty, whitespace, too long and non-ASCII keys are rejected; a valid key round-trips through JSON

## 3. Per-context idempotency policy (RimBank.Postgresql)
- [x] `IdempotentOutcome` document (id `"<command type>:<key>"`, `Fingerprint`, `Acknowledgement` JSON), registered in each context's store schema by `AddServiceDomainStore<TStore, TCore>`. `RecordedAt` was dropped: Marten's `mt_last_modified` records it without needing a `TimeProvider` in the generated code
- [x] `IdempotencyGate.Fingerprint`: SHA-256 of the command's System.Text.Json serialization with `IdempotencyKey` removed
- [x] `CommandIdempotencyPolicy` (an `IChainPolicy` with two JasperFx frames, like `AddWritePort`), applied to the chains in the context's namespace whose message implements `ICommand`. It makes the chain transactional, adds `IdempotencyGate.EnterAsync<TAck>` to the middleware and puts `IdempotencyGate.Remember<TAck>` first among the postprocessors (before `SaveChangesAsync`)
- [x] In-flight concurrency: `pg_try_advisory_xact_lock` in the handler's transaction (open question answered); the loser gets `shared.idempotency-key-in-use`
- [x] Replay and refusals are returned through `MessageContext.EnqueueCascadingAsync`, the same call Wolverine generates for a handler's result (open question answered)
- [x] `tests/RimBank.Postgresql.Tests/Idempotency/IdempotencyTests.cs` (Testcontainers, see validation.md)

## 4. HTTP binder (RimBank.Http)
- [x] New project `src/RimBank.Http/RimBank.Http.csproj` (SharedKernel, `RimBank.Problems`, ASP.NET Core framework reference), added to `RimBank.slnx`, with a lock file
- [x] `IdempotencyKeyHeader`: an endpoint parameter bound with `BindAsync` (it never fails on its own) that adds `IdempotencyKeyRequirement` metadata
- [x] `RequireIdempotencyKey()` on an endpoint or route group: a missing header gives `shared.idempotency-key-missing` and an invalid or repeated one gives `shared.idempotency-key-invalid`, both 400 Problem Details
- [x] `RimBank.Problems`: no change needed. `Conflict` already maps to 409, and the `type` comes from the code (ADR 0015); the 409 responses are asserted in `RimBank.Http.Tests`
- [x] `tests/RimBank.Http.Tests/`: an in-process `TestServer` with test-only endpoints

## 5. Architecture rules and roadmap
- [x] `RimBank.Architecture.Tests`: `http-only-in-api-and-host` and `http-dependencies`, with three bad fixtures (a core and an `.Mcp` using `RimBank.Http`, and `RimBank.Http` using Marten); `RimBank.Http` added to the composition projects, the project references and both runtime lock files
- [x] Tick 0.12 in `specs/roadmap.md`; tick the completed plan and validation items; record the answered open questions in requirements.md
- [x] `dotnet format --verify-no-changes` and the full test run are green locally

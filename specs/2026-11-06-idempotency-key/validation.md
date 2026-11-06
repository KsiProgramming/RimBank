# Validation: Idempotency

## Done when (from roadmap)
> **0.12 Idempotency:** `Idempotency-Key` middleware or policy for commands, with tests (same key gives the same result, different payload gives 409). Queries are naturally idempotent and need no key.
> + Definition of done (roadmap.md)

## Automated checks
Primary proof (chosen at planning): **Testcontainers tests** in `tests/RimBank.Postgresql.Tests/Idempotency/IdempotencyTests.cs` (namespace `RimBank.Postgresql.Idempotency.Tests`), run through `IMessageBus.InvokeAsync<Result<T>>` against the fake contexts:
- [x] `The_same_key_and_payload_returns_the_same_acknowledgement_and_appends_once`: the second call returns an equal acknowledgement; the stream has one posting; the inbox holds one `FakeAmountNoted` message
- [x] `The_same_key_with_a_different_payload_returns_an_idempotency_conflict`: `ErrorKind.Conflict`, code `shared.idempotency-key-reused`; no new event
- [x] `Concurrent_calls_with_the_same_key_commit_once`: 8 parallel calls give one commit; the others are replays or `shared.idempotency-key-in-use`; the stream has one posting
- [x] `A_failed_result_stores_no_outcome_and_can_be_retried_with_the_same_key`: a rejected command (`fake-ledger.ledger-not-found`) stores nothing; a retry with the same key once the ledger exists is decided again
- [x] `The_same_key_in_two_contexts_does_not_clash`: `FakeLedger` and `FakeRegistry` both accept the key; each outcome is in its own schema
- [x] `The_same_key_on_two_command_types_does_not_clash`
- [x] `A_command_without_a_key_is_refused_and_commits_nothing` (`shared.idempotency-key-missing`)
- [x] `Queries_are_not_wrapped_by_the_idempotency_policy`: a query dispatches with no key and stores no outcome
- [x] `Only_the_fingerprint_is_stored_never_the_payload`
- [x] `The_fingerprint_ignores_the_key_and_changes_with_the_payload`

Required by the Definition of done (kept minimal):
- [x] `RimBank.SharedKernel.Idempotency.Tests.IdempotencyKeyTests`: valid and invalid keys, errors, JSON
- [x] `RimBank.Http.Tests`: a missing, invalid or repeated `Idempotency-Key` gives 400 `application/problem+json` with `errorCode` `shared.idempotency-key-missing` / `-invalid`; a valid key reaches the endpoint as sent; a query needs no key; command endpoints carry `IdempotencyKeyRequirement` metadata
- [x] 409 mapping: `shared.idempotency-key-reused` and `shared.idempotency-key-in-use` give 409 with their `urn:rimbank:problem:shared:…` type. This is asserted in `RimBank.Http.Tests` and not in `RimBank.Problems.Tests`, because `RimBank.Problems` needed no change
- [x] `RimBank.Architecture.Tests` green (181), including `http-only-in-api-and-host` and `http-dependencies` with their bad fixtures
- [x] Existing `RimBank.Postgresql.Tests`, `RimBank.Host.Tests` and `RimBank.AppHost.Tests` still green after `ICommand` gained its key. The full solution passes in Release: 694 tests, 0 failed
- [x] `dotnet format --verify-no-changes` and `dotnet restore --locked-mode` pass locally
- [ ] GitHub Actions CI (build, test, format, CodeQL) green. The `linux-x64` lock file of `RimBank.Architecture.Tests` was edited by hand to match `win-x64`, and only CI can confirm it

## Manual / demo checks
- [x] `dotnet test --project tests/RimBank.Postgresql.Tests --filter-namespace "RimBank.Postgresql.Idempotency.Tests"` with Docker running: 10/10 green
- [x] Outcome rows only in the owning context's schema, fingerprint only: asserted by `The_same_key_in_two_contexts_does_not_clash` and `Only_the_fingerprint_is_stored_never_the_payload` (they query `<schema>.mt_doc_idempotentoutcome` as the administrator), so no psql session was needed
- [x] ADR 0018 read against tech-stack §5 and §8.1: no contradiction; both sections now link to it

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 0.12 ticked in the same PR
- [x] ADR 0018 written; tech-stack and CONTRIBUTING updated
- [x] Nothing logs the key or the payload; only the SHA-256 fingerprint and the acknowledgement are stored
- [x] No HTTP endpoint or MCP tool for a real BIAN operation added (that is 1.5 / 1.16), so no parity or conformance test is due

# Validation: Idempotency

## Done when (from roadmap)
> **0.12 Idempotency:** `Idempotency-Key` middleware or policy for commands, with tests (same key gives the same result, different payload gives 409). Queries are naturally idempotent and need no key.
> + Definition of done (roadmap.md)

## Automated checks
Primary proof (chosen at planning): **Testcontainers tests** in `tests/RimBank.Postgresql.Tests/Idempotency/IdempotencyTests.cs` (namespace `RimBank.Postgresql.Idempotency.Tests`), run through `IMessageBus.InvokeAsync<Result<T>>` against the fake contexts:
- [ ] `The_same_key_and_payload_returns_the_same_acknowledgement_and_appends_once`: the second call returns an equal acknowledgement; the stream has one event; the outbox holds one message
- [ ] `The_same_key_with_a_different_payload_returns_an_idempotency_conflict`: `ErrorKind.Conflict`, code `shared.idempotency-key-reused`; no new event
- [ ] `Concurrent_calls_with_the_same_key_commit_once`: N parallel calls give one commit; the others are replays or `shared.idempotency-key-in-use`; the stream has one event
- [ ] `A_failed_result_stores_no_outcome_and_can_be_retried_with_the_same_key`: a rejected command (`fake-ledger.amount-not-positive`) commits nothing; a retry with the same key and a valid state is decided again
- [ ] `The_same_key_in_two_contexts_does_not_clash`: `FakeLedger` and `FakeRegistry` both accept key `k`; each outcome is in its own schema
- [ ] `The_same_key_on_two_command_types_does_not_clash`
- [ ] `Queries_are_not_wrapped_by_the_idempotency_policy`: a query dispatches with no key and stores no outcome

Required by the Definition of done (kept minimal):
- [ ] `RimBank.SharedKernel.Idempotency.Tests.IdempotencyKeyTests`: valid and invalid keys
- [ ] `RimBank.Http.Tests`: a missing or invalid `Idempotency-Key` gives 400 `application/problem+json` with `errorCode` `shared.idempotency-key-missing` / `-invalid`; a valid key reaches the endpoint
- [ ] `RimBank.Problems.Tests`: `shared.idempotency-key-reused` and `shared.idempotency-key-in-use` map to 409 with their documented `type`
- [ ] `RimBank.Architecture.Tests` green, including the `RimBank.Http` dependency rule and its bad fixture
- [ ] Existing `RimBank.Postgresql.Tests`, `RimBank.Host.Tests` and `RimBank.AppHost.Tests` still green after `ICommand` gains its key
- [ ] `dotnet format --verify-no-changes`; GitHub Actions CI (build, test, format, CodeQL) green

## Manual / demo checks
- [ ] `dotnet test tests/RimBank.Postgresql.Tests --filter "FullyQualifiedName~Idempotency"` with Docker running: all green
- [ ] With psql on the test container, after a test run: `select id from <context_schema>.mt_doc_idempotentoutcome` shows the rows only in the owning context's schema, and only the fingerprint hash is stored (no payload)
- [ ] Read ADR 0018 against tech-stack §5 and §8.1: no contradiction

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 0.12 ticked in the same PR
- [ ] ADR 0018 written; tech-stack and CONTRIBUTING updated
- [ ] No idempotency key or payload logged beyond the key itself at debug level; no secrets or PII in logs
- [ ] No HTTP endpoint or MCP tool for a real BIAN operation added (that is 1.5 / 1.16), so no parity or conformance test is due

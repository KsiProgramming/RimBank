# Validation: Wolverine wiring

## Done when (from roadmap)
> **0.11 Wolverine wiring:** command and query dispatch through `IMessageBus` (`InvokeAsync<T>`), local durable queues, transactional outbox with Marten, and a smoke test that sends a command, queries the result, and publishes and handles a message.
> + Definition of done (roadmap.md)

Phase description → checks:
- *Command and query dispatch through `IMessageBus`* → the dispatch tests in group 3
- *Local durable queues* → `The_message_is_delivered_through_the_durable_local_queue_and_handled`, `A_message_left_in_the_outbox_is_delivered_after_restart`
- *Transactional outbox with Marten* → `A_cascaded_message_is_stored_in_the_outbox_with_the_events`, the failed-result test
- *Smoke test* → `A_command_is_dispatched…` + `A_query_returns_the_inline_view…` + `The_message_is_delivered…`, run end to end in one test host

Definition of done items that do not apply: there is no BIAN operation (no MCP parity, no conformance), no HTTP endpoint and no new business error path (no new Problem Details). The CQRS, cross-context isolation, architecture and CI items **do** apply and are covered below. No read model is added or changed (the query reuses the 0.10 inline view, whose projection and rebuild tests already exist).

## Automated checks
- [ ] `tests/RimBank.Postgresql.Tests`, Testcontainers (PostgreSQL 17, two fake contexts):
  - [ ] Dispatch: a command returns its acknowledgement through `InvokeAsync<Result<…>>`; a query returns the inline view right after it; each handler commits through its own context's store
  - [ ] Failed result: comes back as a value (no exception), and neither events nor outgoing messages are committed
  - [ ] Outbox: the cascaded message is saved in the same transaction as the events, in the context's own schema
  - [ ] Durable local queue: the message is handled; a message left in the outbox is delivered after a host restart
  - [ ] Isolation: `rimbank_fake_ledger` cannot read or write `fake_registry`'s envelope tables (SQL state `42501`); the existing 0.10 isolation tests still pass
- [ ] `tests/RimBank.Host.Tests` (in-process, no database): one bus per host; conventional discovery is off; existing composition and Problem Details tests still green
- [ ] `tests/RimBank.Architecture.Tests`:
  - [ ] `wolverine-runtime-only-in-composition` and the updated `shared-postgresql-dependencies` pass on the solution and fail only on their own bad fixtures (violation-isolation theory)
  - [ ] Cores and `.PublishedLanguage` still reference no Wolverine; the fake core parts in tests reference no Wolverine
- [ ] Existing suites green: `RimBank.AppHost.Tests`, `RimBank.Problems.Tests`, `RimBank.SharedKernel.Tests`
- [ ] `dotnet restore RimBank.slnx --locked-mode` (win-x64 and linux-x64 lock files), Release build with warnings as errors, `dotnet format RimBank.slnx --verify-no-changes`
- [ ] NuGet audit: no known vulnerability in `WolverineFx` or `WolverineFx.Marten` (ADR 0013)
- [ ] GitHub Actions `ci` (Testcontainers on `ubuntu-24.04`) and `codeql` green

## Manual / demo checks
Optional, not required to merge:
- [ ] `dotnet test --project tests/RimBank.Postgresql.Tests` with Docker running: all tests pass locally in about a minute
- [ ] While a test is paused, `psql` into the container: `\dt fake_ledger.*` shows the Wolverine envelope tables next to `mt_events`, owned by `rimbank_fake_ledger`, and there is no `wolverine` schema (or, if the fallback in ADR 0017 was needed, it holds only node and agent tables and no context role has grants on it)

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 0.11 ticked in the same PR
- [ ] ADR 0017 written and accepted; tech-stack §2 and §4 and CONTRIBUTING updated
- [ ] No connection strings or message payloads with secrets in logs or test output
- [ ] Fake contexts live only under `tests/` (no speculative context in `src/`); no core-like test code references Wolverine

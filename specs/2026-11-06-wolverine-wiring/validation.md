# Validation: Wolverine wiring

## Done when (from roadmap)
> **0.11 Wolverine wiring:** command and query dispatch through `IMessageBus` (`InvokeAsync<T>`), local durable queues, transactional outbox with Marten, and a smoke test that sends a command, queries the result, and publishes and handles a message.
> + Definition of done (roadmap.md)

Phase description → checks:
- *Command and query dispatch through `IMessageBus`* → `Messaging/DispatchTests`
- *Local durable queues* → `The_message_goes_through_the_durable_inbox_in_the_contexts_own_schema`, `A_message_waiting_in_the_contexts_inbox_is_handled_after_a_restart`
- *Transactional outbox with Marten* → the generated order (enqueue, `SaveChangesAsync`, flush; ADR 0017), `A_failed_result_comes_back_as_a_value_and_commits_neither_events_nor_messages`
- *Smoke test* → `Smoke_a_command_is_queried_and_its_message_is_published_and_handled`, end to end in one test host

Definition of done items that do not apply: there is no BIAN operation (no MCP parity, no conformance), no HTTP endpoint and no new business error path (no new Problem Details). The CQRS, cross-context isolation, architecture and CI items **do** apply and are covered below. No read model is added or changed (the query reuses the 0.10 inline view, whose projection and rebuild tests already exist).

## Automated checks
- [x] `tests/RimBank.Postgresql.Tests`, Testcontainers (PostgreSQL 17, two fake contexts):
  - [x] Dispatch: a command returns its acknowledgement through `InvokeAsync<Result<…>>`; a query returns the inline view right after it, or a `NotFound` error as a value; each handler commits through its own context's store
  - [x] Failed result: comes back as a value (no exception), and neither events nor envelopes are committed
  - [x] Outbox: the cascaded message is saved in the same transaction as the events, in the context's own schema (`fake_ledger.wolverine_incoming_envelopes`, none in `fake_registry` or `wolverine`)
  - [x] Durable local queue: the message is handled; a message waiting in the context's inbox is handled after a host restart
  - [x] Isolation: `rimbank_fake_ledger` cannot read `fake_registry`'s envelope tables nor the `wolverine` schema (SQL state `42501`); `wolverine` is owned by `rimbank_wolverine`; the 0.10 isolation tests still pass
- [x] `tests/RimBank.Postgresql.Tests`, unit: queue name, the reserved `Wolverine` name, a write port without the session constructor is refused, three `ready` checks (two contexts and the host store), and the fake cores never mention Marten or Wolverine
- [x] `tests/RimBank.Host.Tests` (in-process, no database): one bus per host; no handler unless a context includes its assembly; `Balanced` durability; existing composition and Problem Details tests still green
- [x] `tests/RimBank.Architecture.Tests`:
  - [x] `adapters-dispatch-through-the-message-bus` and the updated `shared-postgresql-*` rules pass on the solution and fail only on their own bad fixtures (violation-isolation theory)
  - [x] Cores and `.PublishedLanguage` still reference no Wolverine
- [x] Existing suites green: `RimBank.AppHost.Tests` (real startup with Docker), `RimBank.Problems.Tests`, `RimBank.SharedKernel.Tests` — 636 tests in all
- [x] `dotnet restore RimBank.slnx --locked-mode` (win-x64 and linux-x64 lock files), Release build with warnings as errors, `dotnet format RimBank.slnx --verify-no-changes`
- [x] NuGet audit: no known vulnerability in `WolverineFx`, `WolverineFx.Marten` or `WolverineFx.RuntimeCompilation` (ADR 0013)
- [ ] GitHub Actions `ci` (Testcontainers on `ubuntu-24.04`) and `codeql` green

## Manual / demo checks
Optional, not required to merge:
- [x] `dotnet test --project tests/RimBank.Postgresql.Tests` with Docker running: all tests pass locally in about 30 seconds
- [x] The tables were listed during development: `fake_ledger` and `fake_registry` each hold `wolverine_incoming_envelopes`, `wolverine_outgoing_envelopes` and the dead-letter tables next to `mt_events`; `wolverine` holds only node, agent, control-queue, dead-letter and (unused) envelope tables

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 0.11 ticked in the same PR
- [x] ADR 0017 written and accepted; tech-stack §2, §3.3 and §4 and CONTRIBUTING updated
- [x] No connection strings or message payloads with secrets in logs or test output (role passwords are generated per test run and only travel as bind parameters)
- [x] Fake contexts live only under `tests/` (no speculative context in `src/`); the fake core parts reference no Wolverine (`FakeCoreTests`)

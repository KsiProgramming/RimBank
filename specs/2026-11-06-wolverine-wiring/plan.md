# Plan: Wolverine wiring

Test-first, as in 0.10: each group adds its failing tests first, then the code. The solution builds and every test passes after each group (mission principle 10).

## 1. Packages, ADR draft, architecture rules
- [ ] `docs/adr/0017-wolverine-wiring.md` (status Proposed; accepted in group 5)
- [ ] `Directory.Packages.props`: an `ItemGroup Label="Messaging"` with `WolverineFx` and `WolverineFx.Marten` under one `WolverineVersion` property, compatible with Marten 9.58.0 and ≥ 6.39. Run NuGet audit (ADR 0013)
- [ ] `RimBank.Postgresql.csproj` references `WolverineFx.Marten`; `RimBank.Host.csproj` references `WolverineFx`; lock files regenerated (win-x64, and linux-x64 in the `mcr.microsoft.com/dotnet/sdk:10.0` container as in 0.10)
- [ ] `RimBank.Architecture.Tests`: update `shared-postgresql-dependencies` to allow `Wolverine.Marten`; new rule `wolverine-runtime-only-in-composition` (`WolverineOptions` / `UseWolverine` only in `RimBank.Host` and `RimBank.Postgresql`)
- [ ] `RimBank.Architecture.Fixtures`: bad fixture `ApiConfiguresWolverine.Api` (uses `WolverineOptions`); check the violation-isolation theory
- [ ] `specs/tech-stack.md` §2 and §4 rows; CONTRIBUTING rule rows

## 2. Bus in the host, explicit discovery
- [ ] Tests in `RimBank.Host.Tests`: `The_host_runs_one_message_bus`, `Conventional_handler_discovery_is_off` (a handler in the Host or test assembly is not discovered)
- [ ] `HostComposition`: `UseWolverine` with conventional discovery off, `Durability.Mode = Balanced` (overridable in tests), no main Marten store
- [ ] Check startup with only ancillary stores and no main message store (open question); apply the fallback and note it in ADR 0017 if needed

## 3. Store binding + command and query dispatch
- [ ] `tests/RimBank.Postgresql.Tests/FakeContexts/`: a Wolverine-free core part per fake context: `RecordFakePosting` command (`ICommand`) → handler returning `Result<FakePostingAcknowledged>` plus the event and a cascaded `FakePostingNoted` message; `RetrieveFakeLedger` query (`IQuery<FakeLedgerView>`) → handler reading the inline view through a read port
- [ ] Tests (Testcontainers): `A_command_is_dispatched_to_its_context_and_returns_an_acknowledgement`, `A_query_returns_the_inline_view_right_after_the_command`, `Each_context_handler_commits_through_its_own_store` (the events land in `fake_ledger.mt_events`, not in `fake_registry`), `A_failed_result_comes_back_as_a_value_and_commits_neither_events_nor_messages`
- [ ] `ServiceDomainStoreRegistration`: overload `AddServiceDomainStore<TStore, TCoreMarker>(…)` that calls `IntegrateWithWolverine()`, includes the core assembly in discovery and applies `UseAncillaryStorageFromAssemblyContaining<TCoreMarker>(typeof(TStore))`, plus `AutoApplyTransactions`
- [ ] Resolve the "failed result" open question (Wolverine behaviour, or a small `FailedResultRollbackPolicy` chain policy in `RimBank.Postgresql`)

## 4. Outbox + local durable queue
- [ ] Tests (Testcontainers): `A_cascaded_message_is_stored_in_the_outbox_with_the_events` (same transaction: kill the bus before delivery and the envelope row is in `fake_ledger`'s envelope table), `The_message_is_delivered_through_the_durable_local_queue_and_handled`, `A_message_left_in_the_outbox_is_delivered_after_restart`, `A_context_role_cannot_read_another_contexts_envelope_tables` (SQL state `42501`)
- [ ] Local queue convention: `ServiceDomainName.Queue` (`fake-ledger`) and `opts.LocalQueue(...).UseDurableInbox()` in the registration; routing of the context's own messages to its queue
- [ ] Check that `ServiceDomainDatabase.ProvisionAsync` still covers the envelope tables (they are created in the context's schema by its own role)

## 5. Wrap up
- [ ] ADR 0017 accepted, with the answers to the open questions
- [ ] CONTRIBUTING "Messaging" section: how a context registers its handlers, how to dispatch, the "failed result commits nothing" rule
- [ ] Tick 0.11 in `specs/roadmap.md`; point ADR 0016's "comes in 0.11" line to ADR 0017
- [ ] `dotnet format RimBank.slnx --verify-no-changes`, locked restore, Release build with no warnings, all tests green (including `RimBank.AppHost.Tests`)

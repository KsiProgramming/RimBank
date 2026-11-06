# Plan: Wolverine wiring

Test-first, as in 0.10: each group adds its failing tests first, then the code. The solution builds and every test passes after each group (mission principle 10).

## 1. Packages, ADR draft, architecture rules
- [x] `docs/adr/0017-wolverine-wiring.md`
- [x] `Directory.Packages.props`: an `ItemGroup Label="Messaging"` with `WolverineFx`, `WolverineFx.Marten` and `WolverineFx.RuntimeCompilation` under one `WolverineVersion` property, **6.62.0** (the version built against Marten 9.58.0, and ≥ 6.39). NuGet audit raised nothing. `WolverineFx.RuntimeCompilation` was not in the plan: Wolverine 6 no longer ships the runtime compiler (ADR 0017)
- [x] `RimBank.Postgresql.csproj` references `WolverineFx.Marten`; `RimBank.Host.csproj` references `WolverineFx.RuntimeCompilation` and `RimBank.Postgresql`; lock files regenerated (win-x64 and linux-x64 with the CONTRIBUTING loop)
- [x] `RimBank.Architecture.Tests`: `shared-postgresql-dependencies` allows Wolverine; `shared-postgresql-only-in-postgresql-adapters` allows `RimBank.Host` (for its own message store). The planned rule `wolverine-runtime-only-in-composition` became **`adapters-dispatch-through-the-message-bus`**: `.Api` and `.Mcp` use no Wolverine type but `IMessageBus` (tech-stack §4). It is stricter where it matters and does not block a future `.Subscriptions` adapter that configures its own listeners. `JasperFx.Generated` (the JasperFx source generator's output) is treated as tool-generated
- [x] `RimBank.Architecture.Fixtures`: a `Wolverine.WolverineOptions` stub and bad fixtures `ApiConfiguresWolverine.Api` and `McpConfiguresWolverine.Mcp`; the violation-isolation theory passes
- [x] `specs/tech-stack.md` §2, §3.3 and §4; CONTRIBUTING rule rows and a "Messaging" section

## 2. Bus in the host, explicit discovery
- [x] Tests in `RimBank.Host.Tests/Messaging`: `The_host_runs_one_message_bus`, `No_handler_is_found_unless_a_context_includes_its_own_assembly` (replaces `Conventional_handler_discovery_is_off`: discovery stays conventional, but the application assembly is the host, which holds no handler), `The_bus_is_safe_to_run_on_several_host_instances`
- [x] `MessagingComposition.AddRimBankMessaging` called from `HostComposition`: `UseWolverine` with the host as application assembly, `Durability.Mode = Balanced`, `AutoApplyTransactions`, runtime compilation, no main Marten store
- [x] Startup with only ancillary stores **fails** (Wolverine needs a "main" message store), so the fallback applies: `MessagingNodeRegistration.PersistMessagingNode` (schema `wolverine`, role `rimbank_wolverine`, `ConnectionStrings:Wolverine`, provisioned and health-checked like a context), called inside `UseWolverine` when the connection string is set

## 3. Store binding + command and query dispatch
- [x] `tests/RimBank.Postgresql.Tests/FakeContexts/`: one namespace per fake context (`…FakeContexts.Ledger.Tests`, `…FakeContexts.Registry.Tests`) so each binds to its own store. Wolverine-free core parts: `OpenFakeLedger`, `PostFakeAmount` (returns `Result<FakeLedgerAcknowledged>` plus a cascaded `FakeAmountNoted`), `FakeAmountNotedHandler`, `RetrieveFakeLedger` (inline view through `IFakeLedgerEnquiry`), `RegisterFakeEntry`; `FakeCoreTests` checks that they never mention Marten or Wolverine
- [x] Tests (Testcontainers, `Messaging/DispatchTests`): `A_command_is_dispatched_to_its_context_and_returns_an_acknowledgement`, `A_query_returns_the_inline_view_right_after_the_command`, `A_query_for_an_unknown_record_returns_a_not_found_error_as_a_value`, `Each_context_handler_commits_through_its_own_store`, `A_failed_result_comes_back_as_a_value_and_commits_neither_events_nor_messages`
- [x] `AddServiceDomainStore<TStore, TCore>(…)` replaces the single-generic method (every context uses the bus): `IntegrateWithWolverine` with the context's schema, the core's assembly in discovery, and `UseAncillaryStorageFromNamespace(typeof(TStore), coreNamespace)` (a namespace, not an assembly: it is the context boundary, and the fakes share one assembly). `AutoApplyTransactions` is set once by the host
- [x] Not in the plan: `AddWritePort<TPort, TImplementation>()`. With only ancillary stores, `IDocumentSession` is not in the container, so a Marten write port cannot be resolved. A JasperFx variable source builds it inline from the handler's own session, and a chain policy makes the handler transactional
- [x] "Failed result" answered: a failed decision commits nothing **by construction** (the handler appends and returns messages only on success); no rollback policy is needed

## 4. Outbox + local durable queue
- [x] Tests (Testcontainers, `Messaging/OutboxTests`): `Smoke_a_command_is_queried_and_its_message_is_published_and_handled` (the roadmap smoke test), `The_message_goes_through_the_durable_inbox_in_the_contexts_own_schema`, `A_message_waiting_in_the_contexts_inbox_is_handled_after_a_restart` (a message scheduled in `fake_ledger`'s inbox, host stopped before it is due, handled by the next host). The planned "kill the bus before delivery" test is covered by the generated order (enqueue, then `SaveChangesAsync`, then flush; ADR 0017) and by the failed-result test
- [x] `RoleIsolationTests`: a context role cannot read another context's envelope tables nor the `wolverine` schema (`42501`); the `wolverine` schema is owned by `rimbank_wolverine`
- [x] Local queue convention: `ServiceDomainName.QueueName` (`fake-ledger`), messages from the context's namespace routed to it, `UseDurableInbox`
- [x] `ServiceDomainDatabase.ProvisionAsync` needed no change: the envelope tables are created in the context's schema by its own role

## 5. Wrap up
- [x] ADR 0017 accepted, with the answers to the open questions; ADR 0016 points to it
- [x] CONTRIBUTING "Messaging" section: registering handlers and ports, dispatching, the "failed result commits nothing" rule
- [x] Tick 0.11 in `specs/roadmap.md`
- [x] `dotnet format RimBank.slnx --verify-no-changes`, locked restore, Release build with no warnings, 636 tests green (including `RimBank.AppHost.Tests`)

# Wolverine wiring

Roadmap phase: 0.11
Branch: plan/0.11-wolverine-wiring

## Context
Every `.Api`, `.Mcp` and `.Subscriptions` adapter sends commands and queries through Wolverine's `IMessageBus`, and every integration event leaves a context through a transactional outbox (tech-stack §1, §2, §4, §8.4). Before the first real flow (1.3 Register, 1.5 the first endpoint, 1.13 published language), the wiring those adapters rely on has to exist and be proven.

This phase depends on 0.6 (`IDecider`, `IProjection`), 0.7 (architecture rules and fixtures, ADR 0012 CQRS markers), 0.8 (Host and module entry point, ADR 0014) and 0.10 (per-context Marten stores, ADR 0016). All are merged. It unblocks 0.12 (idempotency is a Wolverine policy), 1.3–1.7 (the first command and query flows), 1.13 (publishing through the outbox) and 4.1 (consuming through the durable inbox).

Relevant constitution:
- [mission.md](../mission.md), principles 2 (hexagonal: the core has no Wolverine reference), 5 (CQRS: commands return acknowledgements, queries read views), 7 (autonomous contexts, "failure stays local"), 9 (production-grade) and 10 (small steps). As in 0.10, no real context exists yet, so the convention is proven with **test-only fake contexts** (non-goal "no speculative coverage").
- [tech-stack.md §1](../tech-stack.md#1-platform): Wolverine for dispatch, outbox/inbox and local durable queues.
- [tech-stack.md §2](../tech-stack.md#2-topology-modular-monolith-first): each context has its own schema, role and store. Contexts talk through durable local queues with an outbox.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): "Core and Wolverine": core handlers are plain classes with `Handle` methods that return their results, with no Wolverine reference. `.Postgresql` may use `Wolverine.Marten`.
- [tech-stack.md §8.1–8.2, §8.4](../tech-stack.md#8-commands-queries--read-models-cqrs): command handlers return `Result<T>` acknowledgements and query handlers return `Result<TView>`. Integration events go through the outbox and are received through the durable inbox.
- ADR 0014 (module entry point), ADR 0016 (ancillary stores, no main store, role isolation; it says the outbox comes in 0.11).

**CQRS split:** this phase adds no BIAN operation. It adds the plumbing for both sides:
- **command side:** `IMessageBus.InvokeAsync<Result<TAck>>(command)` reaches the core command handler. The handler's chain commits through **its own context's** Marten session, and the events and any outgoing messages are saved in one transaction (outbox).
- **query side:** `IMessageBus.InvokeAsync<Result<TView>>(query)` reaches the core query handler, which reads an **inline** view (the fake context's existing inline view from 0.10). No new async view.
- **messaging:** a cascaded message is published to a **local durable queue** and handled after commit.

There is no HTTP or MCP surface and no BIAN service domain, so there is no parity or conformance test in this phase.

## Scope
### In scope
- **Host:** `UseWolverine` once in `HostComposition` (one bus per host). No main Marten store and no main message store. Handler discovery is explicit: conventional scanning is off, and each context's assemblies are added by its own adapters (ADR 0014: the Host module only calls `Add…` extensions).
- **`RimBank.Postgresql` (extended, no new project):** `AddServiceDomainStore<TStore>` also
  - calls `IntegrateWithWolverine()` on the context's ancillary store, so the **envelope tables (outbox and inbox) live in the context's own schema**, owned by its own role;
  - registers a Wolverine extension that binds every handler in the context's core assembly to that store with `opts.Policies.UseAncillaryStorageFromAssemblyContaining<TCoreMarker>(typeof(TStore))`, so the core needs no `[MartenStore]` attribute;
  - turns on transactional middleware for those chains (`AutoApplyTransactions`).
- **Local durable queues:** a convention for naming a context's local queue (`<service-domain>` in kebab case) and making it durable, so messages survive a restart and are retried.
- **Dispatch convention:** commands and queries are dispatched with `InvokeAsync<Result<T>>`. A failed `Result` is a value, not an exception: it comes back to the caller and no events or messages are committed.
- **Fake contexts in tests:** the 0.10 fakes (`FakeLedger`, `FakeRegistry`) gain a core-like assembly or namespace with a command, a query and a handler that returns its result plus a cascaded message, with no Wolverine reference.
- **Architecture rules (each with a bad fixture):** `RimBank.Postgresql` may now use Wolverine; `.Api` and `.Mcp` use Wolverine only through `IMessageBus` (`adapters-dispatch-through-the-message-bus`, which replaced the planned "Wolverine configuration only in composition" rule); cores and `.PublishedLanguage` still reference no Wolverine (existing rules).
- **Constitution:** tech-stack §2 (envelopes per context), §4 (`RimBank.Postgresql` row), CONTRIBUTING "Messaging" section.
- **ADR 0017.**

### Out of scope
- `Idempotency-Key` handling (0.12).
- Authentication and authorisation on dispatch (0.13, 1.15).
- HTTP endpoints and MCP tools that call `IMessageBus` (1.5, 1.16).
- Real integration events, `.PublishedLanguage` schema tests (1.13) and `.Subscriptions` inbox consumers (4.1). The smoke test publishes a test-only message.
- Sagas and scheduled messages (5.3, 4.10).
- RabbitMQ transport (Stage 8+ extraction).
- Wolverine OpenTelemetry activity source and meter, and async daemon or outbox health metrics (7.4). Existing Host and AppHost tests must stay green.
- Production envelope-table migrations (decided with the Marten ones in 7.5).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Where envelope (outbox/inbox) tables live | **In each context's own schema**, through `IntegrateWithWolverine()` on its ancillary store. No shared `wolverine` schema | Keeps ADR 0016's isolation: no role gets grants on another schema. A stuck outbox stays local (principle 7), and extracting a context moves its envelopes with it. Cost: one durability agent per context, accepted. |
| Where the wiring lives | `UseWolverine` in `RimBank.Host`; per-context `IntegrateWithWolverine` and the store-binding policy in `RimBank.Postgresql` | No new project. `Wolverine.Marten` is already allowed in `.Postgresql` (§4), and the store registration is the one place that knows both the store and the context. |
| Binding handlers to their store | `UseAncillaryStorageFromNamespace(typeof(TStore), coreNamespace)` (Wolverine ≥ 6.39; planned by assembly, changed to the namespace during implementation, ADR 0017) | The core stays free of Wolverine attributes (§4 "Core and Wolverine"), and a forgotten attribute cannot commit to the wrong store. |
| Handler discovery | The host, which holds no handler, is the application assembly; each context adds its own assembly (planned as "conventional discovery off", ADR 0017) | Explicit, like `ServiceDomainModules.All` (ADR 0014). No surprise handlers from test or adapter assemblies. |
| Dispatch | `InvokeAsync<Result<T>>` for commands and queries | Matches §8.1–8.2 (`Result` acknowledgements and views). Expected failures stay values (principle 3). |
| Failed command | A failed `Result` commits nothing (no events, no outgoing messages) | Money correctness: a rejected decision must leave no trace in the stream or the outbox. Exact mechanism checked in group 3 (open question). |
| Local queues | One durable local queue per context, named `<service-domain>` | Messages survive restarts and failures are retried, without RabbitMQ (§1). |
| Durability mode | `Balanced` by default; `Solo` in tests | Safe with several host instances, like `DaemonMode.HotCold` in ADR 0016. |

## ADRs to write
- **0017 Wolverine wiring and per-context envelope storage:** one bus per host; envelopes in each context's schema (and why not a shared `wolverine` schema); store binding by core assembly; explicit discovery; `InvokeAsync<Result<T>>` dispatch and the "failed result commits nothing" rule; local durable queue naming; durability mode; what is left for 0.12, 1.13, 4.1 and 7.4.

## Open questions
Answered during implementation (ADR 0017):
- **Failed `Result` and the transaction:** it holds by construction. A core handler appends events and returns messages only when the decision succeeds, so the transactional middleware has nothing to save. `A_failed_result_comes_back_as_a_value_and_commits_neither_events_nor_messages` proves it; no chain policy is needed.
- **No main store:** Wolverine refuses to start without a "main" message store. The fallback applies: the host's own store (schema `wolverine`, role `rimbank_wolverine`, `ConnectionStrings:Wolverine`) holds only node, agent and control tables; every context envelope stays in its context's schema, and no context role has a grant on `wolverine`. It must be registered inside `UseWolverine` (Wolverine rejects service registrations from `ConfigureWolverine`), so the host calls `PersistMessagingNode` from `RimBank.Postgresql`, and `shared-postgresql-only-in-postgresql-adapters` now allows the host.
- **Wolverine version:** 6.62.0, the release built against Marten 9.58.0; it includes the 6.39 storage policies. No known vulnerability.
- **Additional finding:** Wolverine 6 no longer ships the runtime compiler. The host and the tests reference `WolverineFx.RuntimeCompilation` (`TypeLoadMode.Dynamic`); pre-generated code is decided in 7.5.
- **Additional finding:** a write port implemented over `IDocumentSession` cannot come from the container when there is no main Marten store. `AddWritePort<TPort, TImplementation>()` builds it inline in the generated handler from the handler's own session and makes the handler transactional.
- **Additional finding:** the JasperFx source generator adds `JasperFx.Generated.DiscoveredEventModels` to every assembly that references Wolverine; the architecture tests treat it as tool-generated.

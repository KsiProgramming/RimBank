# 0017. Wolverine wiring and per-context envelope storage

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.11

## Context

Every `.Api`, `.Mcp` and `.Subscriptions` adapter sends commands and queries through Wolverine's `IMessageBus`, and every message that leaves a context goes through a transactional outbox ([tech-stack §1, §2, §4, §8.4](../../specs/tech-stack.md)). The core must stay free of Wolverine: its handlers are plain classes with `Handle` methods that take their ports and return their results (§4 "Core and Wolverine").

[ADR 0016](0016-per-context-marten-stores.md) gives each context its own Marten ancillary store, schema and login role, with no main store and no grants on any other schema. Wolverine's documentation recommends one shared `wolverine` schema for the outbox and inbox of a modular monolith, which would give every context role rights on a common schema.

Building the wiring with two test-only fake contexts against Wolverine 6.62.0 turned up four constraints:

1. **Wolverine requires one "main" message store.** With only ancillary stores it refuses to start ("none has been designated as the 'Main' store").
2. **A service-collection extension cannot add a message store.** `services.ConfigureWolverine(...)` runs as an extension registered in the container, and Wolverine refuses service registrations from such extensions. `PersistMessagesWithPostgresql` registers services, so it can only run inside `UseWolverine`.
3. **A port cannot get the handler's session from the container.** With only ancillary stores, `IDocumentSession` is not registered, so a write port implemented over `IDocumentSession` cannot be resolved ("Cannot build service type …"). Wolverine's session priming only covers a registered `IDocumentSession`.
4. **Wolverine 6 no longer ships the runtime compiler.** Handler code is generated, and at startup that needs `WolverineFx.RuntimeCompilation` (Roslyn), unless the code is pre-generated.

## Options considered

Where the outbox and inbox (envelope) tables live:

1. **A shared `wolverine` schema** for every context's envelopes (Wolverine's recommendation). It uses fewer resources, but every context role needs rights on a common schema, which bends ADR 0016, and one context's backlog sits next to every other's.
2. **Each context's own schema**, through `IntegrateWithWolverine` on its ancillary store. Isolation holds and extraction moves the envelopes with the context. The cost is one durability agent per context.

Where Wolverine is configured:

1. **A new `RimBank.Messaging` project.** It is a clean split, but it means one more composition project and more rules, and it would need to know each context's store anyway.
2. **`RimBank.Host` for the bus, and `RimBank.Postgresql` for each context's store integration**, since `AddServiceDomainStore` is the one place that knows both the context and its store.

How a core handler reaches its write port:

1. **The core handler takes `IDocumentSession`**, which breaks §4.
2. **Register a main `AddMarten()` store** so that `IDocumentSession` is in the container. This brings back a privileged default store, which ADR 0016 rejected.
3. **Build the port inline in the generated handler code**: a JasperFx variable source turns a write-port parameter into `new TImplementation(documentSession)`, using the session Wolverine opened on the context's store for that handler.

## Decision

- **One bus per host.** `RimBank.Host` calls `UseWolverine` once (`MessagingComposition.AddRimBankMessaging`). Its application assembly is the host, which holds no handler, so a handler is found only because its context includes its own assembly. Transactions are applied automatically (`AutoApplyTransactions`), and the durability mode is `Balanced` (several host instances share the work; tests use `Solo`).
- **Envelopes in each context's own schema.** `AddServiceDomainStore<TStore, TCore>` calls `IntegrateWithWolverine` with the context's schema, so `wolverine_incoming_envelopes`, `wolverine_outgoing_envelopes` and the dead-letter tables sit next to `mt_events`, owned by the context's role.
- **A host message store for Wolverine's own bookkeeping.** Because a main store is mandatory, the host has one: schema `wolverine`, role `rimbank_wolverine`, connection string `ConnectionStrings:Wolverine`. It is named like a service domain (`MessagingNodeRegistration.MessagingNode`), so it gets the same provisioning and `ready` health check, and the name is reserved. It holds node, agent and control-queue tables; no context envelope goes there, and no context role has any grant on it. The host calls `options.PersistMessagingNode(environment)` inside `UseWolverine` (constraint 2) when `ConnectionStrings:Wolverine` is set; without it, as now with no context, messages stay in memory.
- **Each context binds its own namespace to its own store.** `AddServiceDomainStore<TStore, TCore>` takes `TCore`, any type in the root namespace of the context's core. It adds the core's assembly to handler discovery and calls `UseAncillaryStorageFromNamespace(typeof(TStore), coreNamespace)`, so every handler in `RimBank.X.Y` (and its own adapters' child namespaces) commits through the context's store without a Wolverine attribute in the core. A namespace rather than an assembly, because a namespace is the context's boundary (ADR 0011) and the test fakes share one assembly.
- **Write ports are built from the handler's own session.** `builder.AddWritePort<TPort, TImplementation>()` (in `RimBank.Postgresql`) requires a public `TImplementation(IDocumentSession)` constructor. It adds a variable source so the generated handler builds the port inline from the session it opened on the context's store, and a chain policy that declares the session dependency and makes every handler taking the port transactional. The generated order is: handler, enqueue cascaded messages, `SaveChangesAsync` (events and outgoing envelopes in one transaction), flush. Read ports stay ordinary container services: a query handler opens no session and no transaction.
- **Dispatch with `InvokeAsync<Result<T>>`.** A command handler returns `Result<TAcknowledgement>`, optionally with messages to cascade (a tuple); a query handler returns `Result<TView>`. A failed `Result` comes back as a value. **A failed decision commits nothing by construction:** the handler appends events and returns messages only when the decision succeeds, so the transaction has nothing to save. No extra rollback policy is needed.
- **One durable local queue per context.** Messages from the context's namespace are routed to the queue `<service-domain>` (kebab case, `ServiceDomainName.QueueName`), which uses the durable inbox. A message survives a restart, and a backlog in one context never delays another.
- **Runtime code generation for now.** The host and the tests reference `WolverineFx.RuntimeCompilation` and use `TypeLoadMode.Dynamic`. Pre-generated code (`TypeLoadMode.Static`, no Roslyn in the image) is decided with the container images in 7.5.

## Consequences

- A context's `.Postgresql` adapter calls `AddServiceDomainStore<TStore, TCore>(…)`, `AddWritePort<TPort, TImplementation>()` for each write port, and registers its read ports in the container. Its core never mentions Marten or Wolverine.
- `RimBank.Host` now references `RimBank.Postgresql` (for the host message store) and `WolverineFx.RuntimeCompilation`. The rule `shared-postgresql-only-in-postgresql-adapters` allows the host; `shared-postgresql-dependencies` allows Wolverine. This amends ADR 0016's "used only by `.Postgresql` adapters".
- A new rule, `adapters-dispatch-through-the-message-bus`, enforces tech-stack §4's "Wolverine (`IMessageBus`)" for `.Api` and `.Mcp`: they use no other Wolverine type. Bad fixtures prove it for both. The JasperFx source generator adds a `JasperFx.Generated` type to every assembly that references Wolverine; the architecture tests treat it as tool-generated.
- The first real context (1.4) adds the `Wolverine` connection string and its role password to the AppHost, with its own context connection string. Until then the AppHost and the Host run the bus in memory.
- Production must provision `rimbank_wolverine` and the `wolverine` schema like the context roles, and migrate Wolverine's tables with Marten's (7.5).
- Deferred: `Idempotency-Key` handling (0.12, see [ADR 0018](0018-idempotency-keys-for-commands.md)); consuming another context's integration events through `.Subscriptions` (4.1), including how `.PublishedLanguage` messages are routed (they are currently routed with their context's namespace); sagas and scheduled business messages (4.10, 5.3); Wolverine's OpenTelemetry source and meter, outbox lag and dead-letter metrics (7.4); RabbitMQ when a context is extracted.
- Proven by `tests/RimBank.Postgresql.Tests/Messaging` (dispatch, own-store commits, failed results, the end-to-end smoke test, envelopes in the context's schema, delivery after a restart), the extended role-isolation tests (no access to another context's envelopes or to the `wolverine` schema), and `tests/RimBank.Host.Tests/Messaging`.

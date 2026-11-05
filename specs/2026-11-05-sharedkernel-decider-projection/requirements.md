# SharedKernel: decider & projection abstractions

Roadmap phase: 0.6
Branch: plan/0.6-sharedkernel-decider-projection

## Context
Every service domain is built from two pure halves: a write side (`Decide` / `Evolve` over an event-sourced aggregate) and a read side (`Project` events into a view). This phase fixes both contracts once, in `RimBank.SharedKernel`, together with the test helpers every context will use. After it, an aggregate or a projection reads the same everywhere, and a test of either takes a few lines.

It depends on 0.3 (`Result<T>` / `Error`, merged): a rejected command is a `Result` failure, not an exception. It follows the static-abstract interface style of 0.4 (`ITypedId<TSelf>`, ADR 0008) and reuses the FsCheck setup and `MoneyArbitraries` from 0.5 (ADR 0009). It unblocks 0.7 (architecture tests can check "aggregates implement `IDecider`", "projections implement `IProjection` and are pure"), 0.10 (Marten projections call the pure `Project`), 1.2 (the first aggregate), 1.6 (the first projection) and 3.5 (the "projected balance = aggregate balance" property).

Relevant constitution:
- [mission.md](../mission.md), principle 3: "Decisions are pure functions (`decide(command, state) → events`, `evolve(state, event) → state`)"; expected failures are `Result` values; sealed by default.
- [mission.md](../mission.md), principle 4 (event-sourced truth) and quality bar "Rebuildable reads": a view is a fold of events, so it can always be rebuilt.
- [tech-stack.md §7](../tech-stack.md#7-domain-modelling-conventions): an aggregate is a `sealed record` state with `Evolve`, and decisions are pure static functions.
- [tech-stack.md §8.1–8.2](../tech-stack.md#8-commands-queries--read-models-cqrs): a projection is a pure `(view, event) → view` living in the core; Marten only runs it.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): SharedKernel is BCL only; projections never take ports, handlers or `TimeProvider`.
- [tech-stack.md §9](../tech-stack.md#9-testing-stack): "evolve/decide laws, projection/aggregate agreement" with FsCheck.
- [ADR 0003](../../docs/adr/0003-separate-commands-and-queries.md), consequences: "SharedKernel gains a projection contract and Given/Then-view test helpers (roadmap 0.6)".

**CQRS split:** `IDecider` is the write-side contract (commands → decision → events); `IProjection` is the read-side contract (events → view). This phase adds no command, query, handler, port or BIAN operation. Whether a view is projected inline or async is decided per view (tech-stack §8.3); the `Project` contract is the same for both, so nothing here depends on that choice.

There is no BIAN service domain in this phase.

## Scope
### In scope
- **Write side** — `IDecider<TSelf, TCommand, TEvent>` in `RimBank.SharedKernel`, implemented by the aggregate's state record (`where TSelf : IDecider<TSelf, TCommand, TEvent>`), all members `static abstract`:
  - `static abstract TSelf Initial { get; }`: the state before any event (for example "not registered").
  - `static abstract Result<IReadOnlyList<TEvent>> Decide(TCommand command, TSelf state)`: pure. Success with events (possibly empty, for an idempotent no-op), or a business failure as an `Error`.
  - `static abstract TSelf Evolve(TSelf state, TEvent @event)`: pure, total over the aggregate's own events.
  - `TCommand` and `TEvent` are the aggregate's own base types (for example an `abstract record` per aggregate), matched with **switch expressions** (`.editorconfig` IDE0066).
- **Read side** — `IProjection<TView, TEvent>` in `RimBank.SharedKernel`, implemented by a sealed `…Projection` class (`where TView : class`):
  - `static abstract TView? Project(TView? view, TEvent @event)`: pure. `null` in means "no view yet"; `null` out means "no view" (not created, or removed). Events the view does not care about return the view unchanged.
- **Test helpers** in `tests/RimBank.SharedKernel.Testing` (namespace `RimBank.SharedKernel.Testing`):
  - `DeciderSpecification<TState, TCommand, TEvent>`: `Given(params TEvent[])` → `When(TCommand)` → `Then(params TEvent[])` (an empty `Then()` asserts no events), or `ThenFails(ErrorKind kind, string code)`. The `Given` events are folded with `Initial` + `Evolve`. Failure messages show the given events, the command, and expected vs actual events (or the unexpected error).
  - `ProjectionSpecification<TProjection, TView, TEvent>`: `Given(params TEvent[])` → `Then(TView expected)` or `ThenNoView()`, folding from `null` through `Project`.
  - `DeciderLaws` / `ProjectionLaws` (FsCheck property helpers a context calls with its own command and event generators):
    - **Determinism:** the same command on the same state gives the same result; folding the same events twice gives equal states and views.
    - **Decided events evolve:** for any reachable state and any command, every event `Decide` returns can be applied by `Evolve` without throwing.
    - **Projection/aggregate agreement:** for any sequence of commands, the view projected from the accepted events equals a context-supplied mapping of the aggregate state (`Func<TState, TView?>`). This is the law 3.5 reuses for balances.
- **Sample decider and projection** in `tests/RimBank.SharedKernel.Tests/DecidersAndProjections/` (namespace `RimBank.SharedKernel.DecidersAndProjections.Tests`): a tiny `SampleLedger` (open with a currency, post a `Money` amount, close; rejects posting to a closed ledger and a currency mismatch) and its `SampleLedgerView` / `SampleLedgerViewProjection`. They exist only to exercise the contracts and helpers, like `SampleId` in 0.4.
- **ADR 0010**: decider and projection contracts, with a usage sample that also lives as a compiling test.

### Out of scope
- Command handlers, write ports, read ports and queries: 1.3 and 1.6.
- Event metadata (stream version, timestamps, correlation ID) in `Evolve` / `Project`: Marten supplies it in 0.10; the ETag in 1.7. If a view needs one of them, it comes from event data or an adapter, decided then.
- A public `Fold` / `Rebuild` API in SharedKernel and an `IsTerminal` member for closed records: not needed yet (the helpers fold internally; rebuild is Marten's job in 0.10). Revisit in 4.11 (facility closure) if needed.
- Wiring the projection contract into Marten (`SingleStreamProjection`, async daemon, rebuild): 0.10.
- ArchUnitNET rules for these contracts: 0.7 (this phase adds the rule text to the roadmap).
- Snapshots of long streams: 3.1 / 3.4.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Contract shape | Static-abstract interfaces: `IDecider<TSelf, TCommand, TEvent>`, `IProjection<TView, TEvent>` | User choice. Matches "decisions are pure static functions" (tech-stack §7) and `ITypedId<TSelf>` (ADR 0008). Static members cannot capture services or `TimeProvider`, so purity is structural, not by convention. A record of delegates was rejected: less discoverable and closures can hide dependencies |
| Who implements `IDecider` | The aggregate state `sealed record`; `Decisions.cs` is a `partial` of it | Keeps the tech-stack §3.4 file layout (`Entry.cs` = state + evolve, `Decisions.cs` = decide) while giving one type the compiler can check |
| `Decide` return type | `Result<IReadOnlyList<TEvent>>` | Business rejections are values (principle 3, ADR 0006); an empty list is an accepted no-op (idempotent repeat) |
| State before the first event | `static abstract TSelf Initial` | No `null` state on the write side; "does not exist yet" is a real state the decisions can match on |
| View before the first event | `TView?` (`null`) | Maps directly to Marten's "create / delete document"; a view does not need a fake empty instance |
| Time in decisions | Not a parameter: the command carries the timestamps the handler takes from `TimeProvider` | `Decide` stays pure and replayable; time is still injected (tech-stack §7), at the handler |
| Unknown event in `Evolve` | The default switch arm throws `UnreachableException` | An aggregate's events are its own closed set; an unknown one is a programmer error (ADR 0006) |
| Unknown event in `Project` | Returns the view unchanged | A view may listen to a subset of events, and new events must not break old views |
| Event comparison in helpers | Value equality (`Equals`) of records, with a readable expected/actual message | Events are records; see open question on collection-valued fields |
| Laws | FsCheck helpers in `RimBank.SharedKernel.Testing`, parameterised by the context's own generators | User choice. tech-stack §9; reused by 1.2, 3.3 and 3.5 |
| Build order | Test-first, 4 groups, one commit each | User choice; same rhythm as 0.3–0.5 |

## ADRs to write
- **0010: Decider and projection contracts**: the two interfaces and their members, why static abstract (purity, ADR 0008 style), `Initial` vs `null` view, time in commands, unknown-event rules, where the files go in a core (tech-stack §3.4), the helper API, the laws, and a usage sample that also lives as `DeciderUsageSampleTests`.

## Open questions
- **Events with collection fields**: a `record` holding a `List<T>` or array is not value-equal, so `Then(...)` would fail on equal events. Options: require `ImmutableArray<T>`/a value-equal collection in events (and later enforce it in 0.7), or compare events by their JSON form in the helpers. Answer in group 3 and record it in ADR 0010.
- **Generic inference**: check in group 1 that `DeciderSpecification<SampleLedger, SampleLedgerCommand, SampleLedgerEvent>` is the shortest form C# allows; if a `For<TState>()` entry point can infer the other two type arguments from the interface, use it.

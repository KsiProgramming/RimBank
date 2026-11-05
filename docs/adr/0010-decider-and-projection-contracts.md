# 0010. Decider and projection contracts

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.6

## Context

Mission principle 3 says "Decisions are pure functions (`decide(command, state) → events`, `evolve(state, event) → state`)". `specs/tech-stack.md` §7 makes an aggregate a `sealed record` state with `Evolve` and pure static decisions, and §8.2 makes a projection a pure `(view, event) → view` that lives in the core and that Marten only runs (inline or async, §8.3). ADR 0003 asks SharedKernel for a projection contract and Given/Then-view test helpers. Every context from Stage 1 on writes aggregates and views, so their shape and the way they are tested should be fixed once.

Constraints: `RimBank.SharedKernel` references the BCL only (§4). Business failures are `Result` values (ADR 0006). Projections must not use ports, handlers or `TimeProvider` (§4). The typed identifier pattern already uses a static-abstract interface (`ITypedId<TSelf>`, ADR 0008).

## Options considered

1. **Convention only.** Aggregates and projections follow naming rules with no shared type. Nothing for the compiler or the 0.7 architecture tests to check, and every context writes its own test helpers.
2. **A decider as a record of delegates** (`Decider(Initial, Decide, Evolve)`). Composable, but delegates can capture services or a clock, it is less discoverable, and the state type and its rules live apart.
3. **Static-abstract interfaces** implemented by the state record and by a projection class. Static members cannot capture anything, so purity is structural; generic helpers can call `TState.Decide` directly.

## Decision

Option 3.

### Write side: `IDecider<TSelf, TCommand, TEvent>`

```csharp
public interface IDecider<TSelf, TCommand, TEvent> where TSelf : IDecider<TSelf, TCommand, TEvent>
{
    static abstract TSelf Initial { get; }
    static abstract Result<IReadOnlyList<TEvent>> Decide(TCommand command, TSelf state);
    static abstract TSelf Evolve(TSelf state, TEvent domainEvent);
}
```

- Implemented by the aggregate state `sealed record`. `<Aggregate>.cs` holds the state and `Evolve`; `Decisions.cs` is a `partial` of the same record holding `Decide` (tech-stack §3.4).
- `TCommand` and `TEvent` are the aggregate's own base types (an `abstract record` each), matched with switch expressions.
- **`Initial`** is a real state ("not registered", "not opened"), never `null`, so decisions can match on it.
- **`Decide`** returns the events to append, or the `Error` that rejects the command. An empty list is an accepted no-op (for example closing a closed record), which keeps repeated commands idempotent. Build the result with `Decision.Accept<TEvent>(…)` or `Decision.Nothing<TEvent>()`; an `Error` converts implicitly. The factory exists because C# does not apply user-defined conversions to interface types.
- **Time and new identifiers are not parameters.** The handler takes them from `TimeProvider` (and `XId.New`) and puts them in the command, so `Decide` stays pure and replayable.
- **`Evolve`** is total over the aggregate's own events. An unknown event is a programmer error: the default switch arm throws `UnreachableException`.

### Read side: `IProjection<TView, TEvent>`

```csharp
public interface IProjection<TView, TEvent> where TView : class
{
    static abstract TView? Project(TView? view, TEvent domainEvent);
}
```

- Implemented by a `sealed class …Projection` (private constructor) next to its view in the query-side business flow (tech-stack §3.4). The view is an immutable record.
- `null` in means there is no view yet; `null` out means there is no view (not created, or removed). This maps onto Marten's create and delete in 0.10.
- An event the view does not care about returns the view **unchanged**, so a new event never breaks an existing view.
- The same pure function serves an inline and an async projection; that choice is made per view (§8.3), not here. A view is the fold of its events from `null`, which is exactly what a rebuild does.
- Event metadata (stream version, timestamps, correlation ID) is not passed in. A view that needs one takes it from event data, or the adapter adds it (0.10, 1.7).
- The parameter is named `domainEvent` in both contracts because `CA1716` rejects `@event` on interface members.

### Test helpers (`tests/RimBank.SharedKernel.Testing`)

- `DeciderSpecification<TState, TCommand, TEvent>`: `Given(events…).When(command).Then(events…)`, `Then()` for a no-op, or `ThenFails(kind, code)` (returns the `Error`). Given events are folded from `Initial` with `Evolve`.
- `ProjectionSpecification<TProjection, TView, TEvent>`: `Given(events…).Then(view)` or `ThenNoView()`, folding from `null`.
- A failure is a `ShouldAssertException` whose message lists the given events, the command, and expected vs actual events, error or view.
- C# does not infer type arguments from constraints, so a test file names the three types once with a `using` alias.
- **Equality**: events and views are compared with `Equals`. A record field that is a collection (including `ImmutableArray<T>`) compares by reference, so an event or view that holds a collection must give itself value equality (override `Equals` / `GetHashCode`, as `Error` does). When expected and actual print the same but are not equal, the helpers say so. A reusable value-equal collection waits for the first event that needs one.

### Laws (`DeciderLaws<TState, TCommand, TEvent>`, FsCheck)

Each law runs random command sequences from `Initial` using the context's own `Gen<TCommand>`; rejected commands stay in the sequence, and a failing sequence shrinks by dropping commands.

| Law | Meaning |
|---|---|
| `DecisionsAreDeterministic` | The same commands always give the same events, errors and states |
| `DecidedEventsEvolve` | Every event `Decide` returns, from any reachable state, can be applied by `Evolve` |
| `ProjectionAgreesWithState<TProjection, TView>` | After every command, the view projected from the accepted events equals a context-supplied mapping of the state (for example "projected balance = aggregate balance", reused in 3.5) |

## Usage sample

The sample ledger lives in `tests/RimBank.SharedKernel.Tests/DecidersAndProjections/`, and the tests below are kept compiling as `DeciderUsageSampleTests.cs` there:

```csharp
// SampleLedger.cs: state + evolve
public sealed partial record SampleLedger(SampleLedgerStatus Status, Money? Balance, int Postings)
    : IDecider<SampleLedger, SampleLedgerCommand, SampleLedgerEvent>
{
    public static SampleLedger Initial { get; } = new(SampleLedgerStatus.NotOpened, null, 0);

    public static SampleLedger Evolve(SampleLedger state, SampleLedgerEvent domainEvent) => domainEvent switch
    {
        SampleLedgerOpened opened => state with { Status = SampleLedgerStatus.Open, Balance = Money.Zero(opened.Currency) },
        SampleLedgerPosted posted => state with { Balance = state.Balance!.Value + posted.Amount, Postings = state.Postings + 1 },
        SampleLedgerClosed => state with { Status = SampleLedgerStatus.Closed },
        _ => throw new UnreachableException($"SampleLedger does not know the event {domainEvent}."),
    };
}

// SampleLedger.Decisions.cs: pure decisions (excerpt)
public static Result<IReadOnlyList<SampleLedgerEvent>> Decide(SampleLedgerCommand command, SampleLedger state) =>
    (command, state.Status) switch
    {
        (OpenSampleLedger open, SampleLedgerStatus.NotOpened) => Decision.Accept<SampleLedgerEvent>(new SampleLedgerOpened(open.Currency)),
        (OpenSampleLedger, _) => _alreadyOpened,
        // …
        (CloseSampleLedger, SampleLedgerStatus.Closed) => Decision.Nothing<SampleLedgerEvent>(),
        (CloseSampleLedger, _) => Decision.Accept<SampleLedgerEvent>(new SampleLedgerClosed()),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "SampleLedger does not know this command."),
    };

// SampleLedgerViewProjection.cs: pure projection
public static SampleLedgerView? Project(SampleLedgerView? view, SampleLedgerEvent domainEvent) => (view, domainEvent) switch
{
    (null, SampleLedgerOpened opened) => new SampleLedgerView(Money.Zero(opened.Currency), 0, false),
    (null, _) => null,
    (_, SampleLedgerPosted posted) => view with { Balance = view.Balance + posted.Amount, Postings = view.Postings + 1 },
    (_, SampleLedgerClosed) => view with { IsClosed = true },
    _ => view,
};

// Tests, with `using LedgerSpecification = DeciderSpecification<SampleLedger, SampleLedgerCommand, SampleLedgerEvent>;`
LedgerSpecification.Given(new SampleLedgerOpened(eur))
    .When(new PostToSampleLedger(tenEuros))
    .Then(new SampleLedgerPosted(tenEuros));

LedgerViewSpecification.Given(new SampleLedgerOpened(eur), new SampleLedgerPosted(tenEuros))
    .Then(new SampleLedgerView(tenEuros, Postings: 1, IsClosed: false));

[Property]
public Property The_view_agrees_with_the_ledger() =>
    LedgerLaws.ProjectionAgreesWithState<SampleLedgerViewProjection, SampleLedgerView>(commands, ExpectedView);
```

## Consequences

- Every aggregate and every view in every context has the same shape, and their tests take a few lines each.
- Purity is checked by the compiler for the parts that matter: a static `Decide` or `Project` cannot reach an injected service or clock.
- 0.7 can check by type: every aggregate state implements `IDecider`, every `…Projection` implements `IProjection`, and projections do not depend on ports, handlers or `TimeProvider`.
- 0.10 wires Marten projections that call the pure `Project`; a rebuild is the same fold the helpers use.
- Events and views that hold collections must define value equality; the helpers point this out when it is missing.
- Not added yet: a public `Fold` / `Rebuild` API, an `IsTerminal` member for closed records (revisit in 4.11), and snapshots of long streams (3.1 / 3.4).

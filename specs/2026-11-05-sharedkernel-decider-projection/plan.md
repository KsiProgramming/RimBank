# Plan: SharedKernel: decider & projection abstractions

Each group is one commit. The solution builds and the tests pass after each one. Groups 1 to 3 are written test-first (red → green → refactor). `IDecider` is the write-side contract and `IProjection` the read-side contract; no command handler, query handler, port or read store is added in this phase.

## 1. Write side: `IDecider` (test-first)
- [ ] `tests/RimBank.SharedKernel.Tests/DecidersAndProjections/SampleLedger.cs`: `sealed partial record SampleLedger : IDecider<SampleLedger, SampleLedgerCommand, SampleLedgerEvent>` with `Initial` (not opened), `Evolve` (switch expression; default arm throws `UnreachableException`); `SampleLedgerCommand` (`Open(Currency)`, `Post(Money)`, `Close`) and `SampleLedgerEvent` (`Opened`, `Posted`, `Closed`) as `abstract record` hierarchies
- [ ] `.../DecidersAndProjections/SampleLedger.Decisions.cs`: `Decide` returning `Result<IReadOnlyList<SampleLedgerEvent>>`; rejects posting before open or after close (`Conflict`, `sample.ledger-closed`), a currency mismatch (`Validation`, `shared.currency-mismatch` via `Money.TryAdd`), and closing twice is an accepted no-op (empty list)
- [ ] `.../DecidersAndProjections/SampleLedgerDecisionTests.cs`: plain xUnit + Shouldly tests calling `Decide` / `Evolve` directly (the helpers come in group 3)
- [ ] `src/RimBank.SharedKernel/IDecider.cs`: `IDecider<TSelf, TCommand, TEvent>` with `static abstract` `Initial`, `Decide`, `Evolve`; XML docs stating purity, the `Result` rule and the no-op rule
- [ ] Answer the generic-inference open question with a scratch test; note the result in requirements.md
- [ ] Check locally: Release build (warnings as errors), test, `dotnet format --verify-no-changes`

## 2. Read side: `IProjection` (test-first)
- [ ] `.../DecidersAndProjections/SampleLedgerView.cs`: `sealed record SampleLedgerView(Money Balance, int Postings, bool IsClosed)`
- [ ] `.../DecidersAndProjections/SampleLedgerViewProjection.cs`: `sealed class SampleLedgerViewProjection : IProjection<SampleLedgerView, SampleLedgerEvent>`, `Project` as a switch expression; `Opened` creates the view, unhandled events return it unchanged
- [ ] `.../DecidersAndProjections/SampleLedgerViewProjectionTests.cs`: plain tests: `null` + `Opened` creates; `Posted` adds; an event before `Opened` leaves `null`
- [ ] `src/RimBank.SharedKernel/IProjection.cs`: `IProjection<TView, TEvent>` (`where TView : class`), `static abstract TView? Project(TView? view, TEvent @event)`; XML docs on purity, `null` meaning and unknown events

## 3. Test helpers and laws (test-first, dogfooded)
- [ ] `tests/RimBank.SharedKernel.Testing/DeciderSpecification.cs`: `Given(...)` / `When(...)` / `Then(...)` / `ThenFails(kind, code)`; folds from `Initial`; failure message lists given events, command, expected vs actual
- [ ] `tests/RimBank.SharedKernel.Testing/ProjectionSpecification.cs`: `Given(...)` / `Then(view)` / `ThenNoView()`
- [ ] `tests/RimBank.SharedKernel.Testing/DeciderLaws.cs` and `ProjectionLaws.cs`: FsCheck properties for determinism, decided-events-evolve, and projection/aggregate agreement (`Func<TState, TView?>` mapping), taking the caller's `Arbitrary<TCommand>`
- [ ] Rewrite the group 1–2 sample tests with the helpers (`SampleLedgerSpecificationTests.cs`, `SampleLedgerViewSpecificationTests.cs`)
- [ ] `.../DecidersAndProjections/SampleLedgerLawsProperties.cs`: runs the three laws with a `SampleLedgerCommand` arbitrary built on `MoneyArbitraries`; agreement mapping `state → view` (balance, postings, closed)
- [ ] `.../DecidersAndProjections/SpecificationHelperTests.cs` (helper self-tests): wrong events, missing events, unexpected success, unexpected failure, wrong error code, wrong view and an unexpected view each fail with a `ShouldAssertException` whose message names expected and actual
- [ ] Answer the "events with collection fields" open question (value-equal collections vs JSON comparison); record it in requirements.md
- [ ] Update the `RimBank.SharedKernel.Testing.csproj` comment (0.6 helpers now present)

## 4. ADR, docs and roadmap
- [ ] `docs/adr/0010-decider-and-projection-contracts.md` (MADR, status Accepted): the decisions table, both open-question answers, and a usage sample (aggregate + projection + one `Given/When/Then` test) that also lives as `.../DecidersAndProjections/DeciderUsageSampleTests.cs`
- [ ] `CONTRIBUTING.md`: a short "Aggregates and projections" note: implement `IDecider` on the state record, put decisions in a `partial`, keep time in commands, implement `IProjection` on a sealed `…Projection` class, test with `DeciderSpecification` / `ProjectionSpecification` and run the laws
- [ ] `specs/tech-stack.md` §3.3: update the `RimBank.SharedKernel.Testing` comment to name the helpers (only if wording changes)
- [ ] `specs/roadmap.md`: tick **0.6**; add to 0.7 "every aggregate state implements `IDecider`, every `…Projection` implements `IProjection`"

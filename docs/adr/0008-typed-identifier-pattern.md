# 0008. Typed identifier pattern

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.4

## Context

Mission principle 3 forbids primitive obsession, and `specs/tech-stack.md` §7 says identifiers are "strongly typed `readonly record struct` wrapping `Guid` v7". Every aggregate from Stage 1 onwards needs one (`PartyReferenceDataDirectoryEntryId` in 1.2, `ProductDirectoryEntryId` in 2.1, …). IDs are bound from HTTP routes and MCP inputs, stored as Marten stream IDs, and serialized in events and views. The pattern has to be fixed once, before the first context copies it.

Constraints: `RimBank.SharedKernel` references the BCL only (§4). Time always comes from `TimeProvider` (§7). `Decide` is pure (§8.1). Expected failures are `Result` values (ADR 0006).

## Options considered

1. **A third-party library** (Vogen, StronglyTypedId). Least code, but it breaks the BCL-only rule for SharedKernel and puts a code-generator dependency in every core.
2. **Our own Roslyn source generator** (`[TypedId]` on a partial struct). Very little code per ID, but it adds a generator project, analyzer packaging and a harder-to-debug build, to save a couple of dozen short lines per type.
3. **A static-abstract interface plus a helper**, with each ID hand-written. Every ID is a short `readonly record struct` that forwards to shared code, and a reusable contract test checks it.

## Decision

Option 3.

- **`ITypedId<TSelf>`** (`RimBank.SharedKernel`) has `Guid Value` and the static members `New(TimeProvider)`, `From(Guid)`, `TryCreate(Guid)` and `TryCreate(string?)`. It extends `IEquatable`, `IComparable`, `IComparisonOperators`, `ISpanParsable` and `ISpanFormattable`. Parsing, formatting and `CompareTo` are **default implementations on the interface**, so generic code (model binding, `Order()`, `$"{id}"`) works without per-ID code. Each ID still declares the four comparison operators, because `IComparisonOperators` requires them (and analyser CA1036 asks for them). Equality and `==`/`!=` come from the record struct.
- **`TypedId`** holds the shared logic: `NewGuid`, `IsValid`, `EnsureValid`, `TryCreate`, `Parse`/`TryParse`, `Format`/`TryFormat`, `Compare` and `CreatedAt`.
- **Valid values:** non-empty and `Version == 7` only, everywhere (`From`, `TryCreate`, parsing, JSON). Clients cannot send nil or v4 GUIDs, so every stored ID is time-ordered.
- **Trusted vs untrusted input:** `From` and `Parse` throw (`ArgumentException`, `FormatException`). They are for values we wrote ourselves, such as event replay. `TryCreate` returns `Error.Validation("shared.invalid-identifier", "The value is not a valid identifier.")`, which becomes a 400 Problem Details in 0.9. The message never echoes the input.
- **Creating IDs:** `New(TimeProvider)` uses `Guid.CreateVersion7(time.GetUtcNow())`. IDs are created in the command handler (or by the caller) and passed into the command. **`Decide` never creates an ID**, so it stays pure, and a replay never creates new ones.
- **Text form:** exactly the 36-character lower-case `D` format, with no prefix. Formatting accepts only `D` (or no format). Parsing accepts upper- or lower-case hex and nothing else: no braces, no `N` form, no surrounding white space (which `Guid` parsing would otherwise trim).
- **Ordering:** by `Guid` order, which for v7 is creation time to the millisecond and matches PostgreSQL `uuid` ordering. IDs created in the same millisecond have no defined order. **`CreatedAt`** returns the embedded instant (milliseconds). It is informational only: business times always come from events, never from IDs.
- **JSON:** `TypedIdJsonConverterFactory` (System.Text.Json, which ships with the shared framework) writes a plain GUID string, also as a dictionary key. Invalid input throws `JsonException`. Each adapter registers the factory once (0.10 Marten, 0.11 Wolverine, 1.5 ASP.NET Core, 0.15 MCP).
- **`default(TId)`** cannot be prevented for a struct. It holds `Guid.Empty`, which every validating path rejects, and the contract tests document this.
- **Tests:** every ID gets one line, `public sealed class XIdContractTests : TypedIdContractTests<XId>;`, from the new test-only library `tests/RimBank.SharedKernel.Testing`. That library references `xunit.v3.extensibility.core` (the same version as `xunit.v3`) because it is not a test executable. Inherited `[Fact]`s are discovered in the derived class. Phase 0.6 adds its decider and projection helpers to the same library.

## Declaration template

Copy this next to the aggregate (`<Aggregate>/<Aggregate>Id.cs`) and rename `SampleId`:

```csharp
public readonly record struct SampleId : ITypedId<SampleId>
{
    private SampleId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SampleId New(TimeProvider time) => new(TypedId.NewGuid(time));

    public static SampleId From(Guid value) => new(TypedId.EnsureValid(value));

    public static Result<SampleId> TryCreate(Guid value) => TypedId.TryCreate<SampleId>(value);

    public static Result<SampleId> TryCreate(string? text) => TypedId.TryCreate<SampleId>(text);

    public override string ToString() => TypedId.Format(Value);

    public static bool operator <(SampleId left, SampleId right) => TypedId.Compare(left, right) < 0;

    public static bool operator <=(SampleId left, SampleId right) => TypedId.Compare(left, right) <= 0;

    public static bool operator >(SampleId left, SampleId right) => TypedId.Compare(left, right) > 0;

    public static bool operator >=(SampleId left, SampleId right) => TypedId.Compare(left, right) >= 0;
}
```

## Usage sample

From `tests/RimBank.SharedKernel.Tests/TypedIds/TypedIdUsageSampleTests.cs`, which compiles and runs in CI:

```csharp
// Pure decision: it receives the identifier, it never creates one.
static Result<SampleRegistered> Decide(RegisterSample command) =>
    new SampleRegistered(command.Id, command.Name);

// Command handler: creates the identifier from the injected clock, then decides.
static Result<SampleRegistered> Handle(string name, TimeProvider time) =>
    Decide(new RegisterSample(SampleId.New(time), name));

// Query side: untrusted route or tool input becomes a typed identifier or a Validation error.
static Result<string> Retrieve(string? routeValue) =>
    SampleId.TryCreate(routeValue).Map(id => $"sample {id}");
```

## Consequences

- A new ID is the template above (about 25 short lines) plus one contract test line. A mistake in the forwards, such as a missing `EnsureValid`, fails the contract tests.
- `IParsable` and `ISpanParsable` are implemented explicitly on the interface, so `SampleId.TryParse(...)` cannot be called directly on the type; callers use `TryCreate`. Phase 1.7 checks that ASP.NET Core route binding finds the `IParsable` implementation. If it does not, the template gains a one-line public `TryParse` forward, and this ADR is updated.
- `New` matches a Visual Basic keyword, so CA1716 is suppressed on that member only, as for `Error` in ADR 0006. RimBank is C# only.
- Phase 0.7 adds an architecture rule that every `ITypedId<T>` implementation is a `readonly record struct`.
- If the number of IDs ever makes the boilerplate a real cost, a source generator can replace the hand-written forwards without changing the public shape. That would need a new ADR.

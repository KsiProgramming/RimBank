# SharedKernel: typed identifiers

Roadmap phase: 0.4
Branch: plan/0.4-sharedkernel-typed-ids

## Context
Every aggregate from Stage 1 onwards has an identity: `PartyReferenceDataDirectoryEntryId` (1.2), `ProductDirectoryEntryId` (2.1), `FinancialPositionLogId` (3.2), and so on. This phase sets the single pattern they all follow, so that 1.2 and later phases only declare an ID and do not design one.

It depends on 0.3 (`Result<T>` and `Error`, merged): untrusted input that is not a valid ID becomes a `Validation` error value, not an exception. It unblocks 0.6 (deciders take and return typed IDs), 0.10 (Marten stream IDs and JSON serialization), 1.2 (the first real ID), and 1.7 / 1.16 (route and MCP input binding through `IParsable`).

Relevant constitution:
- [mission.md](../mission.md), principle 3: no primitive obsession ("strongly typed IDs"); types are sealed and immutable; expected failures are `Result` values.
- [tech-stack.md §7](../tech-stack.md#7-domain-modelling-conventions): "strongly typed `readonly record struct` wrapping `Guid` v7"; time always comes from `TimeProvider`.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): `RimBank.SharedKernel` references the BCL only. `System.Text.Json` ships in the shared framework, so a converter is allowed and needs no package.
- [tech-stack.md §3.4](../tech-stack.md#34-inside-a-core-project-business-names-only): each ID lives next to its aggregate (for example `PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntryId.cs`), not in SharedKernel.
- [tech-stack.md §8.1](../tech-stack.md#81-write-side): `Decide` is pure, so new IDs are created in the command handler (or by the caller) with `TimeProvider`, never inside `Decide`.
- ADR 0006: error code convention (`shared.invalid-identifier`).

There is no BIAN service domain in this phase. It adds no commands, queries or read models. It provides the identity type that both sides use: commands carry it, and views are keyed by it.

## Scope
### In scope
- `ITypedId<TSelf>` in `RimBank.SharedKernel` (namespace `RimBank.SharedKernel`): `Guid Value { get; }`, `static abstract TSelf From(Guid value)`. It extends `IEquatable<TSelf>`, `IComparable<TSelf>`, `ISpanParsable<TSelf>` and `ISpanFormattable`.
- `TypedId` static helper with the shared logic every ID forwards to:
  - `New<TId>(TimeProvider time)`: `Guid.CreateVersion7(time.GetUtcNow())`
  - `TryCreate<TId>(Guid value) → Result<TId>`: rejects `Guid.Empty` and any GUID whose `Version != 7` with `Error.Validation("shared.invalid-identifier", …)`
  - `TryCreate<TId>(string? text) → Result<TId>`: the same after parsing, for HTTP and MCP input
  - `Parse` / `TryParse` (string and span) for `IParsable` / `ISpanParsable`
  - `CreatedAt(TId id) → DateTimeOffset`: reads the 48-bit Unix-millisecond timestamp of a v7 GUID
- `From(Guid)` validates the same way and throws `ArgumentException` (programmer error, trusted input such as event replay). `TryCreate` is the path for untrusted input.
- Ordering: `CompareTo` follows `Guid` order, which for v7 is creation-time order at millisecond resolution. Order within the same millisecond is not defined, and the docs say so.
- Formatting: `ToString()` gives the lower-case `D` format (`0190f5c4-…`), the same as the `Guid` value, with no prefix.
- `TypedIdJsonConverterFactory` (`System.Text.Json`): reads and writes any `ITypedId<T>` as a plain GUID string, also as a dictionary key (`ReadAsPropertyName` / `WriteAsPropertyName`). An invalid value throws `JsonException`.
- The hand-written declaration pattern (about 25 short lines per ID, including the comparison operators), shown by a `SampleId` in the tests and in ADR 0008.
- `tests/RimBank.SharedKernel.Testing`: a reusable `TypedIdContractTests<TId>` that every future ID test class inherits.
- ADR 0008 (typed identifier pattern).

### Out of scope
- Any real business ID (`PartyReferenceDataDirectoryEntryId` comes in 1.2). SharedKernel holds no business types.
- Registering the JSON converter with Marten or ASP.NET Core: 0.10 (Marten), 0.11 (Wolverine) and 1.5 (API).
- Marten stream identity configuration (Guid streams): 0.10.
- A source generator or third-party library (Vogen, StronglyTypedId): rejected, see Decisions.
- FsCheck properties: FsCheck arrives in 0.5. Example-based tests cover the round trips here.
- The ArchUnitNET rule that every `ITypedId` is a `readonly record struct` in its aggregate folder: 0.7 (noted there as a follow-up).
- Prefixed or human-readable IDs (for example `pty_…`): not requested by BIAN, which uses plain references.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Declaration mechanism | Static-abstract interface `ITypedId<TSelf>` plus a `TypedId` helper; each ID is a hand-written `readonly record struct` that forwards to the helper | User choice. BCL only (tech-stack §4), no build tooling, the code is explicit and easy to read. The boilerplate is small and the contract tests check it |
| Source generator / Vogen / StronglyTypedId | Rejected | A generator adds a project and build complexity. Third-party libraries break the BCL-only SharedKernel rule |
| Creating a new ID | `TypedId.New<TId>(TimeProvider)` (each ID exposes `New(TimeProvider)`) using `Guid.CreateVersion7(DateTimeOffset)` | tech-stack §7: time comes from `TimeProvider`, so tests can use `FakeTimeProvider` and get a known timestamp |
| Where IDs are created | In the command handler or by the caller, then passed into the command or decision | `Decide` stays pure and deterministic (tech-stack §8.1). Replays never create IDs |
| Accepted values | Non-empty and `Version == 7` only, for `From`, `TryCreate` and `Parse` | User choice. Clients cannot send v4 or nil GUIDs, so "ordered by creation time" holds for every stored ID |
| Untrusted vs trusted input | `TryCreate` / `TryParse` return failure (`Result` / `bool`); `From` / `Parse` throw | Expected failures are values (principle 3, ADR 0006). Invalid trusted input is a programmer error |
| Error code | `shared.invalid-identifier`, kind `Validation` | ADR 0006 code convention. It becomes a 400 Problem Details in 0.9 |
| `default(TId)` | Can't be prevented for a struct. It holds `Guid.Empty`, and the contract tests document that `TryCreate(default(TId).Value)` fails | Known C# limitation, stated in ADR 0008 |
| Ordering | `IComparable<TSelf>` through `Guid.CompareTo` | User choice. v7 ordering matches PostgreSQL `uuid` ordering, which helps paging later (3.5) |
| Creation time | `TypedId.CreatedAt(id)` returns the v7 timestamp (millisecond precision) | User choice. It's informational: business times always come from events, never from IDs (ADR 0008) |
| JSON | `TypedIdJsonConverterFactory` in SharedKernel, plain GUID string, property-name support | User choice. STJ is part of the shared framework, so SharedKernel stays BCL only |
| Reusable tests | New `tests/RimBank.SharedKernel.Testing` class library with `TypedIdContractTests<TId>`; 0.6 adds its Given/When/Then helpers to it | Future context test projects inherit the checks. It's a test-only project, so tech-stack §3.3 is updated to list it |

## ADRs to write
- **0008: Typed identifier pattern**: `readonly record struct` + `ITypedId<TSelf>`; v7 only; `New(TimeProvider)`; IDs created outside `Decide`; `TryCreate` vs `From`; the `default` limitation; ordering and `CreatedAt` semantics (informational only); JSON shape; why there is no source generator or library; the declaration template.

## Open questions
- ~~**xUnit package for a test library**~~ Answered in group 1: `xunit.v3.extensibility.core` 4.0.2 (the same version as `xunit.v3`). Inherited `[Fact]`s from `TypedIdContractTests<TId>` are discovered in the derived class in another assembly (11 contract facts run for `SampleId`). Recorded in ADR 0008.
- ~~**Default static implementations**~~ Answered in group 1: yes. `ITypedId<TSelf>` implements the `IParsable`, `ISpanParsable`, `ISpanFormattable` and `IComparable` members as defaults, so each ID only declares `New`, `From`, `TryCreate` (×2), `ToString` and, because CA1036 asks for them, the four comparison operators. `ITypedId<TSelf>` also extends `IComparisonOperators<TSelf, TSelf, bool>`, so the compiler enforces the operators and the contract tests can check them. Recorded in ADR 0008. Follow-up for 1.7: confirm that ASP.NET Core route binding finds the explicit `IParsable` implementation.

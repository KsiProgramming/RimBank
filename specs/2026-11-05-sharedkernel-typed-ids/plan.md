# Plan: SharedKernel: typed identifiers

Each group is one commit. The solution builds and the tests pass after each one. Groups 2 to 4 are written test-first (red → green → refactor).

## 1. Interface, helper skeleton and test-support project
- [ ] `src/RimBank.SharedKernel/ITypedId.cs`: `ITypedId<TSelf> where TSelf : struct, ITypedId<TSelf>`, extending `IEquatable<TSelf>`, `IComparable<TSelf>`, `ISpanParsable<TSelf>`, `ISpanFormattable`, with `Guid Value { get; }` and `static abstract TSelf From(Guid value)`
- [ ] Answer the open question on static virtual defaults: try default implementations of the `IParsable` / `ISpanParsable` members on `ITypedId<TSelf>`. Keep whichever form builds cleanly with `latest-recommended` analysers
- [ ] `src/RimBank.SharedKernel/TypedId.cs`: static class, empty except for the error code constant `shared.invalid-identifier`
- [ ] `tests/RimBank.SharedKernel.Testing/RimBank.SharedKernel.Testing.csproj`: class library, `IsPackable=false`, references SharedKernel, Shouldly and the xUnit extensibility package (answer the open question; add `PackageVersion` to `Directory.Packages.props`)
- [ ] `RimBank.slnx`: add the project under `/tests/`; `RimBank.SharedKernel.Tests` references it
- [ ] Check locally: restore, Release build (warnings as errors), test, `dotnet format --verify-no-changes`

## 2. Creation and validation (test-first)
- [ ] `tests/RimBank.SharedKernel.Tests/TypedIds/SampleId.cs`: the hand-written declaration pattern (`readonly record struct SampleId : ITypedId<SampleId>`, private constructor, `New(TimeProvider)`, `From`, `TryCreate`, forwards to `TypedId`)
- [ ] `tests/.../TypedIds/TypedIdCreationTests.cs`: `New` with a fixed `TimeProvider` gives a v7, non-empty value whose timestamp equals the provider's time (millisecond precision); two `New` calls give different values; `From(Guid.Empty)` and `From(v4)` throw `ArgumentException`; `TryCreate(Guid.Empty)` and `TryCreate(v4)` fail with a `Validation` error, code `shared.invalid-identifier`; `TryCreate(v7)` succeeds; value equality and `GetHashCode`; `default(SampleId).Value == Guid.Empty`, and `TryCreate` of it fails
- [ ] `TypedId.New<TId>`, `TypedId.TryCreate<TId>(Guid)`, and the shared validation `TypedId.IsValid(Guid)` (non-empty and `Version == 7`)
- [ ] XML docs on `ITypedId<TSelf>` and `TypedId`, including "create IDs outside `Decide`"

## 3. Parsing, formatting, ordering and creation time (test-first)
- [ ] `tests/.../TypedIds/TypedIdParsingTests.cs`: `ToString()` is lower-case `D`; `Parse(ToString())` round-trips; `TryParse` / `TryCreate(string?)` fail for null, empty, garbage, the nil GUID and a v4 GUID; span overloads behave the same; `TryFormat` writes the same text; format and provider arguments are accepted and ignored except `D`/`null` (others throw `FormatException`)
- [ ] `tests/.../TypedIds/TypedIdOrderingTests.cs`: IDs created at increasing `TimeProvider` times sort in creation order (`CompareTo`, `Order()`); `TypedId.CreatedAt` returns the creation instant truncated to milliseconds
- [ ] `TypedId.TryCreate<TId>(string?)`, `Parse`, `TryParse` (string and span), `Format` / `TryFormat`, `Compare`, `CreatedAt`
- [ ] `SampleId` forwards to them (or inherits the defaults from group 1)

## 4. JSON converter (test-first)
- [ ] `tests/.../TypedIds/TypedIdJsonTests.cs`: with the factory in `JsonSerializerOptions.Converters`, a `SampleId` serializes to `"0190…"` (a plain string); it deserializes back; a `Dictionary<SampleId, int>` round-trips with GUID property names; a record holding a `SampleId?` handles `null`; an invalid string, a v4 GUID and a number throw `JsonException`
- [ ] `src/RimBank.SharedKernel/TypedIdJsonConverterFactory.cs` (`CanConvert`: struct type implementing `ITypedId<T>` for itself) and `TypedIdJsonConverter<TId>` (`Read`, `Write`, `ReadAsPropertyName`, `WriteAsPropertyName`), both sealed

## 5. Reusable contract tests
- [ ] `tests/RimBank.SharedKernel.Testing/TypedIdContractTests.cs`: `abstract class TypedIdContractTests<TId> where TId : struct, ITypedId<TId>` with `[Fact]`s for the creation, validation, parsing round-trip, ordering and JSON rules above (using a fixed `TimeProvider`)
- [ ] `tests/.../TypedIds/SampleIdContractTests.cs : TypedIdContractTests<SampleId>`. Check that the inherited facts run (the MTP test count goes up)
- [ ] Remove the duplicated assertions from groups 2 to 4 where the contract covers them; keep `SampleId`-specific and helper-level tests

## 6. ADR, docs and roadmap
- [ ] `docs/adr/0008-typed-identifier-pattern.md` (MADR, status Accepted): the decisions table from requirements.md, the declaration template (copied from `SampleId`), and a usage sample (handler creates `New(time)`, `TryCreate(routeValue)` → `Result`). The sample also lives as a compiling test
- [ ] `specs/tech-stack.md` §3.3: list `tests/RimBank.SharedKernel.Testing/` (shared test helpers: typed ID contract, later decider/projection helpers). No new technology, so this edit is covered by ADR 0008
- [ ] `CONTRIBUTING.md`: a short "Typed identifiers" note: copy the template, inherit `TypedIdContractTests<TId>`, never create IDs inside `Decide`
- [ ] `specs/roadmap.md`: tick **0.4 SharedKernel: typed identifiers**; add to 0.7 the rule "every `ITypedId<T>` implementation is a `readonly record struct`"

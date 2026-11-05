# Plan: SharedKernel: Money & Currency

Each group is one commit. The solution builds and the tests pass after each one. Groups 2 to 4 are written test-first (red → green → refactor). Every type is a write-side and read-side value: there are no commands, queries, handlers or projections in this phase.

## 1. FsCheck and test-support generators
- [ ] Convert `Directory.Packages.props` to UTF-8 (no BOM), with no other change (open question)
- [ ] Answer the FsCheck open question: add `FsCheck` and its xUnit v3 integration to `Directory.Packages.props` (testing section), check that a trivial `[Property]` is discovered and run under MTP. Otherwise use `Prop.ForAll(...)` inside `[Fact]`
- [ ] `tests/RimBank.SharedKernel.Testing/RimBank.SharedKernel.Testing.csproj`: reference FsCheck
- [ ] `tests/RimBank.SharedKernel.Tests/RimBank.SharedKernel.Tests.csproj`: reference FsCheck (and the integration package)
- [ ] `tests/RimBank.SharedKernel.Testing/MoneyArbitraries.cs`: placeholder class with the generator registration pattern (filled in groups 2 and 3)
- [ ] Check locally: restore, Release build (warnings as errors), test, `dotnet format --verify-no-changes`

## 2. Currency and the ISO 4217 table (test-first)
- [ ] `tests/RimBank.SharedKernel.Tests/Money/CurrencyTests.cs`: `EUR`/`USD` → 2, `JPY`/`KRW` → 0, `BHD`/`KWD`/`TND` → 3; numeric codes (`EUR` 978); `TryCreate` fails with `Validation` `shared.invalid-currency` for `null`, `""`, `"eur"`, `" EUR"`, `"EURO"`, `"ABC"`, `"XAU"`, `"XDR"`, `"CLF"`; `From` throws `ArgumentException` for the same; equality and `ToString()`; `default(Currency)` is documented as invalid
- [ ] `tests/.../Money/CurrencyTableSnapshotTests.cs`: a checked-in snapshot of `Currency.All` (code, numeric, minor units), so any table change shows up in review; every code is 3 upper-case letters, none starts with `X`, all minor units are in {0, 2, 3}
- [ ] `src/RimBank.SharedKernel/Currency.cs`: `readonly record struct Currency`, private constructor, `Code`, `NumericCode`, `MinorUnits`, `TryCreate`, `From`, `All`, `ToString`
- [ ] `src/RimBank.SharedKernel/Iso4217.cs` (internal): the `FrozenDictionary<string, Currency>` table, with the ISO 4217 List One edition date in a comment
- [ ] `src/RimBank.SharedKernel/CurrencyJsonConverter.cs` + tests: reads and writes `"EUR"`; an unknown code or a non-string throws `JsonException`
- [ ] `MoneyArbitraries`: `Arbitrary<Currency>` drawn from `Currency.All`, weighted so 0, 2 and 3 minor units all appear

## 3. Money arithmetic and rounding (test-first, properties)
- [ ] `tests/.../Money/MoneyArithmeticTests.cs` (examples): `Of`, `Zero`, `+`, `−`, unary `−`, comparisons, `Multiply`; operators with mixed currencies throw `InvalidOperationException`; `TryAdd`/`TrySubtract` with mixed currencies fail with `shared.currency-mismatch`; overflow throws `OverflowException`
- [ ] `tests/.../Money/MoneyRoundingTests.cs` (examples): `Round()` is `ToEven` (`2.345 EUR` → `2.34`, `2.355 EUR` → `2.36`); `Round(AwayFromZero)`; `JPY` rounds to whole units, `BHD` to 3 decimals; `IsRoundedToMinorUnits`
- [ ] `tests/.../Money/MoneyLawsProperties.cs` (FsCheck): `+` is commutative and associative; `Zero` is the identity; `a − a = Zero`; `−(−a) = a`; `a + b − b = a`; comparison agrees with `Amount` comparison; `Round` is idempotent and stays within half a minor unit; a rounded value has `IsRoundedToMinorUnits`; mixed currencies never combine
- [ ] `src/RimBank.SharedKernel/Money.cs`: `readonly record struct Money(decimal Amount, Currency Currency)` with the members above; `IComparable<Money>`, `IComparisonOperators`, `IAdditionOperators`, `ISubtractionOperators`, `IUnaryNegationOperators`, `IMultiplyOperators<Money, decimal, Money>`
- [ ] `MoneyArbitraries`: `Arbitrary<Money>` (rounded and unrounded), and same-currency pair and triple generators, with amounts bounded so the laws cannot overflow `decimal`

## 4. Allocation, parsing, formatting and JSON (test-first)
- [ ] `tests/.../Money/MoneyAllocationTests.cs` (examples): `10.00 EUR`.`Split(3)` → `3.34, 3.33, 3.33`; `Allocate([70, 20, 10])`; `JPY` and `BHD`; negative amounts; zero; unrounded amount, empty ratios, a zero or negative ratio, or `Split(0)` throws `ArgumentException`
- [ ] `tests/.../Money/MoneyAllocationProperties.cs` (FsCheck): for any rounded money and any positive ratios, the parts sum exactly to the original, are all rounded to minor units, keep the currency, and each differs from its exact share by less than one minor unit
- [ ] `tests/.../Money/MoneyParsingTests.cs`: `TryCreate("10.50", "EUR")` succeeds; `"10"`, `"-0.01"`, `"0"` succeed; `"10,50"`, `"1,000.00"`, `"1e3"`, `" 10"`, `""`, `null`, `"10.505"` for `EUR` and `"1.5"` for `JPY` fail with `shared.invalid-amount` (field `amount`); a bad code fails with `shared.invalid-currency` (field `currency`)
- [ ] `tests/.../Money/MoneyFormattingTests.cs` + property: `ToString()` = `"10.50 EUR"`, `"1000 JPY"`, `"1.250 BHD"`; formatting ignores the current culture (run under `fr-FR`); `TryCreate(FormatAmount(), Code)` round-trips any rounded money
- [ ] `tests/.../Money/MoneyJsonTests.cs` + property: `{ "amount": "10.50", "currency": "EUR" }` both ways; a JSON number amount, a missing field, too many decimals or an unknown currency throws `JsonException`; JSON round-trips any money (rounded and unrounded)
- [ ] `Money.Allocate`, `Money.Split`, `Money.TryCreate(string?, string?)`, `ToString`, `FormatAmount`, and `src/RimBank.SharedKernel/MoneyJsonConverter.cs` (sealed)

## 5. ADR, docs and roadmap
- [ ] `docs/adr/0009-money-and-currency.md` (MADR, status Accepted): the decisions table from requirements.md, the ISO 4217 source edition, the FsCheck answer, and a usage sample (`TryCreate` → `TryAdd` → `Split` → `Round`) that also lives as `tests/.../Money/MoneyUsageSampleTests.cs`
- [ ] `CONTRIBUTING.md`: a short "Money" note: never `double`/`float`, use `TryCreate` at boundaries, round explicitly with a named mode, divide with `Allocate`/`Split`, reuse `MoneyArbitraries` in property tests
- [ ] `specs/roadmap.md`: tick **0.5 SharedKernel: Money & Currency**; add to 0.7 the rule "no `double` or `float` fields, properties or parameters in cores, SharedKernel or `.PublishedLanguage`"

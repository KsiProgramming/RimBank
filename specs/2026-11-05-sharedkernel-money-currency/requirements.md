# SharedKernel: Money & Currency

Roadmap phase: 0.5
Branch: plan/0.5-sharedkernel-money-currency

## Context
Every money-moving phase depends on one correct `Money` type: product terms (2.1), balances and transaction capture (3.2–3.5), deposits, withdrawals, holds, fees and interest (4.6–4.10), payment orders (5.1) and double-entry postings (6.2). This phase fixes that type once, so later phases use it and don't redesign it.

It depends on 0.3 (`Result<T>` / `Error`, merged): untrusted amounts and currency codes become `Validation` errors, not exceptions. It follows the patterns of 0.4 (ADR 0008): a `readonly record struct`, `TryCreate` for untrusted input and throwing factories for trusted input, a `System.Text.Json` converter in SharedKernel, and reusable test support in `RimBank.SharedKernel.Testing`. It unblocks 0.6 (deciders over money), 3.3 (property "balance = sum of transactions") and 6.2 (property "debits = credits"), which reuse the FsCheck generators added here.

Relevant constitution:
- [mission.md](../mission.md), quality bar "Money correctness": `decimal` only, ISO 4217 currency and minor-unit rules, explicit rounding, no money created or lost.
- [mission.md](../mission.md), principle 3: immutable value objects, no primitive obsession, expected failures are `Result` values.
- [tech-stack.md §7](../tech-stack.md#7-domain-modelling-conventions): `Money(decimal Amount, Currency Currency)` with ISO 4217 minor units and an explicit rounding policy; `double` / `float` never used for amounts.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): SharedKernel is BCL only, so the ISO 4217 table is embedded in code, not a package.
- [tech-stack.md §9](../tech-stack.md#9-testing-stack): FsCheck for money arithmetic properties. FsCheck is already chosen, so adding it needs no new ADR (ADR 0007: "projects add … FsCheck in their own phase").
- BIAN v14 (`doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/CurrentAccount.yaml`, `PositionKeeping.yaml`): `Amount` = `AmountValue` (string, dot decimal separator) + `AmountCurrency` (`CurrencyCode`, "three contiguous letters", ISO 4217); "a zero amount is considered a positive amount". The JSON shape below follows this.

There is no BIAN service domain in this phase. It adds no commands, queries or read models: `Money` is a value type that both sides use (commands and events carry it, views display it).

## Scope
### In scope
- **`Currency`** (`readonly record struct`, namespace `RimBank.SharedKernel`): `Code` (3 upper-case letters), `NumericCode`, `MinorUnits` (0, 2 or 3).
  - An embedded ISO 4217 List One table of **currencies** (a `FrozenDictionary`), from the SIX edition published 2026-09-17, noted in the file and the ADR. It has 155 of the 178 codes. Excluded are the 10 fund codes (`IsFund="true"`: `BOV`, `CHE`, `CHW`, `CLF`, `COU`, `MXV`, `USN`, `UYI`, `UYW`, `XAD`) and the 13 codes whose minor units are "N.A." (metals, bond units, SDR, `XSU`, `XUA`, `XTS`, `XXX`). Real currencies whose code starts with `X` (`XAF`, `XOF`, `XPF`, `XCD`, `XCG`) are **included**.
  - `Currency.TryCreate(string? code) → Result<Currency>`: exact match only (no trimming or upper-casing). Unknown, lower-case, obsolete or excluded codes fail with `Error.Validation("shared.invalid-currency", …)`.
  - `Currency.From(string code)` throws `ArgumentException` (trusted input, for example event replay). `Currency.All` lists the table.
  - `ToString()` gives the code. A JSON converter reads and writes `"EUR"`.
- **`Money`** (`readonly record struct Money(decimal Amount, Currency Currency)`):
  - `Money.Of(decimal, Currency)` and `Money.Zero(Currency)`. Any scale and sign are allowed (negative balances, unrounded intermediate interest); sign rules belong to each context.
  - Same-currency `+`, `−`, unary `−`, `<`, `<=`, `>`, `>=`, `CompareTo`. A currency mismatch on an operator throws `InvalidOperationException` (programmer error). `TryAdd` / `TrySubtract` return `Result<Money>` failing with `Error.Validation("shared.currency-mismatch", …)` for paths fed by untrusted input.
  - `Multiply(decimal factor)` (and `*`) returns an **unrounded** result. There is no `/` operator: division goes through `Allocate`.
  - Rounding: `Round(MidpointRounding mode)` rounds to the currency's minor units. `Round()` uses `MidpointRounding.ToEven` (banker's rounding), documented. `IsRoundedToMinorUnits` tells whether the amount needs rounding.
  - Allocation: `Allocate(ReadOnlySpan<int> ratios)` and `Split(int parts)` return parts whose sum is **exactly** the original. Parts differ from their exact share by less than one minor unit, and the remainder goes one minor unit at a time to the first parts. The amount must already be rounded to minor units, otherwise it throws.
  - Untrusted input: `Money.TryCreate(string? amount, string? currencyCode) → Result<Money>` parses with the invariant culture and a dot separator only (no thousands separators, exponents or whitespace). It rejects more decimal places than the currency's minor units with `Error.Validation("shared.invalid-amount", …)`. Field errors are keyed `amount` / `currency`.
  - Formatting: `ToString()` gives `"10.50 EUR"` (invariant culture, at least `MinorUnits` decimals). `FormatAmount()` gives `"10.50"`.
  - JSON: `MoneyJsonConverter` writes `{ "amount": "10.50", "currency": "EUR" }`, with the amount as a **string** (as BIAN's `AmountValue` is) so no client parses it as a float. An invalid amount or currency throws `JsonException`.
- **Test support** in `tests/RimBank.SharedKernel.Testing`: FsCheck `Arbitrary<Currency>` and `Arbitrary<Money>` (rounded and unrounded variants, same-currency pairs and triples) in a `MoneyArbitraries` class that later context tests reuse.
- **FsCheck** added to `Directory.Packages.props` (`FsCheck` and its xUnit v3 integration), used by `RimBank.SharedKernel.Tests` and `RimBank.SharedKernel.Testing`.
- **ADR 0009**: Money & Currency.

### Out of scope
- Exchange rates and currency conversion: not on the near-term roadmap. A later phase adds them with its own ADR.
- Percentage / interest-rate value objects: 4.10 (Interest BQ) or 2.1 (product terms), when they are first needed.
- Registering the JSON converters with Marten, Wolverine or ASP.NET Core: 0.10, 0.11 and 1.5.
- Mapping BIAN's `Amount` request models to `Money` in `.Api` / `.Mcp`: each context's adapter phases (for example 4.4, 4.6).
- Problem Details mapping of `shared.invalid-currency`, `shared.invalid-amount` and `shared.currency-mismatch`: 0.9 (Validation → 400).
- Per-context sign rules (for example "a deposit is positive"): 3.3 and 4.6.
- The ArchUnitNET rule forbidding `double` / `float` in money-related members: noted as a follow-up for 0.7.
- Updating the ISO 4217 table automatically: the table changes rarely, and a change goes through a reviewed PR (see the snapshot test).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Scope | Core + allocation (not minimal, not FX) | User choice. Allocation is the only way to divide money without creating or losing a minor unit (quality bar), and 4.9 / 4.10 / 6.x need it |
| `Currency` type | `readonly record struct` backed by an embedded ISO 4217 table | User choice. Same pattern as typed IDs (ADR 0008). BCL only (tech-stack §4) |
| `default(Currency)` / `default(Money)` | Can't be prevented for a struct. `default(Currency)` has a `null` code; operations on it throw, and `TryCreate` never returns it | Same known C# limitation as ADR 0008, stated in ADR 0009 |
| Which codes | Every List One code except fund codes (`IsFund`) and codes with "N.A." minor units, so minor units are 0, 2 or 3 | A retail core never holds a balance in `XAU` or `CLF`. Accepting them would add 4-decimal and "N.A." minor-unit cases with no use. The rule follows ISO's own flags, not the `X` prefix, because `XAF`, `XOF`, `XPF`, `XCD` and `XCG` are real currencies (found while building the table; the first draft wrongly excluded all `X**` codes) |
| Code matching | Exact, upper-case only | Input is either valid or rejected; silently fixing `eur` hides client bugs. Matches BIAN "three contiguous letters" |
| Amount scale | `Money` holds any `decimal` scale; `TryCreate` (untrusted input) rejects more decimals than `MinorUnits` | Interest and fee calculations need unrounded intermediates. Client input with `10.123 EUR` is a mistake, not something to round silently |
| Currency mismatch | Operators throw `InvalidOperationException`; `TryAdd` / `TrySubtract` return `shared.currency-mismatch` (Validation) | Inside a decision, mixing currencies is a bug. At an input boundary it's a client error, which must be a value (principle 3, ADR 0006) |
| Multiplication | Returns an unrounded result | Rounding stays explicit, at the point the business rule names it (quality bar) |
| Default rounding | `Round()` = `MidpointRounding.ToEven`; `Round(mode)` for any other mode | User choice. Banker's rounding has no upward bias over many postings. Contexts that need another mode (for example a fee rounded away from zero) name it at the call site |
| Allocation remainder | Distributed one minor unit at a time to the first parts; works for negative amounts | Deterministic and simple to test; the sum is always exact |
| Division | No `/` operator | `a / 3` can't be exact; `Split(3)` makes the remainder visible |
| JSON shape | `{ "amount": "10.50", "currency": "EUR" }`, amount as string | Follows BIAN `AmountValue` (string); avoids float parsing in JSON clients |
| Parsing | Invariant culture, dot separator, optional leading `-`, no exponent, grouping or whitespace | One unambiguous format, the same as BIAN's "decimal separator is a dot" |
| Error codes | `shared.invalid-currency`, `shared.invalid-amount`, `shared.currency-mismatch`, all `Validation` | ADR 0006 code convention. They become 400 Problem Details in 0.9 |
| Property tests | FsCheck with its xUnit v3 integration; generators in `RimBank.SharedKernel.Testing` | User choice. tech-stack §9; 3.3 and 6.2 reuse them |
| Build order | Test-first, 5 groups, one commit each | User choice; same rhythm as 0.3 and 0.4 |

## ADRs to write
- **0009: Money & Currency**: the ISO 4217 table and which codes are included (with its source edition), exact code matching, the scale rule (any scale inside, minor units at the boundary), mismatch handling (exception vs `Result`), unrounded multiplication, `ToEven` as the default and explicit modes, allocation and its remainder rule, no division operator, the JSON shape, the `default` limitation, the error codes, and a usage sample (parse → add → split → round) that also lives as a compiling test.

## Open questions
- **FsCheck package for xUnit v3**: confirm in group 1 which FsCheck 3.x package integrates with `xunit.v3` 4.0.2 on Microsoft.Testing.Platform (`[Property]` discovered and run), or else use `Prop.ForAll(...).QuickCheckThrowOnFailure()` inside `[Fact]`. Record the answer in ADR 0009.
- **`Directory.Packages.props` encoding**: the file is not UTF-8 (the `§` in a comment is Latin-1). Group 1 converts it to UTF-8 (no BOM) in a separate, explicit change, so that later edits by tools don't corrupt it.

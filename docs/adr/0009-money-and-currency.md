# 0009. Money and Currency

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.5

## Context

The mission's quality bar says: "`decimal` only, with ISO 4217 currency and minor-unit rules. Rounding rules are explicit, and no money is created or lost." `specs/tech-stack.md` §7 fixes the shape `Money(decimal Amount, Currency Currency)` and forbids `double` and `float` for amounts. Every money-moving phase uses this type: product terms (2.1), balances and transaction capture (3.x), deposits, withdrawals, holds, fees and interest (4.6–4.10), payment orders (5.1) and double-entry postings (6.2). It is also stored in events and views, so its JSON shape is hard to change later.

BIAN v14 (`CurrentAccount.yaml`, `PositionKeeping.yaml`) models an amount as `AmountValue` (a string with a dot decimal separator) plus `AmountCurrency` (`CurrencyCode`: "three contiguous letters", ISO 4217), and states that "a zero amount is considered a positive amount".

Constraints: `RimBank.SharedKernel` references the BCL only (§4). Expected failures are `Result` values, and error codes follow `<context>.<error-name>` (ADR 0006). Value types follow the typed ID pattern of ADR 0008 (`readonly record struct`, `TryCreate` for untrusted input, a throwing factory for trusted input).

## Options considered

1. **A third-party money library** (for example NodaMoney). Ready made, but it breaks the BCL-only rule for SharedKernel, brings its own rounding defaults and JSON shape, and puts a dependency at the centre of every money-moving core.
2. **`decimal` plus a currency string** passed side by side. No new types, but it is the primitive obsession principle 3 forbids, and nothing stops adding euros to dollars or keeping a third decimal for EUR.
3. **Hand-written `Currency` and `Money` value types** in SharedKernel, with an embedded ISO 4217 table, explicit rounding, allocation and a BIAN-shaped JSON form.

## Decision

Option 3.

### Currency

- `readonly record struct Currency` with `Code`, `NumericCode` and `MinorUnits`. `Currency.TryCreate(string?)` returns `Result<Currency>`; `Currency.From(string)` throws `ArgumentException` (trusted input). `Currency.All` lists the table, ordered by code. JSON: a plain string such as `"EUR"`, also as a dictionary key.
- **The table** is ISO 4217 List One, **SIX edition published 2026-09-17** (`list-one.xml`), embedded in `Currency.Iso4217.cs`. It keeps **155 of the 178 codes**. It leaves out the 10 fund codes (`IsFund="true"`: `BOV`, `CHE`, `CHW`, `CLF`, `COU`, `MXV`, `USN`, `UYI`, `UYW`, `XAD`) and the 13 codes whose minor units are "N.A." (`XAG`, `XAU`, `XBA`, `XBB`, `XBC`, `XBD`, `XDR`, `XPD`, `XPT`, `XSU`, `XTS`, `XUA`, `XXX`). So minor units are always 0, 2 or 3. The rule follows ISO's own flags, not the `X` prefix: `XAF`, `XOF`, `XPF`, `XCD` and `XCG` are real currencies and are included.
- **Codes match exactly**: upper case, no trimming. `eur` is rejected rather than fixed, so client bugs stay visible.
- A table change (a new edition) is a reviewed PR that updates `Currency.Iso4217.cs` and the snapshot `tests/RimBank.SharedKernel.Tests/MoneyAndCurrency/Iso4217.snapshot.csv` together. A withdrawn code that still appears in stored events needs its own ADR before it is removed.

### Money

- `readonly record struct Money` with `Amount` (`decimal`) and `Currency`, created with `Money.Of(amount, currency)` or `Money.Zero(currency)`. **Any sign and any scale** are allowed: balances can be negative, and interest is calculated unrounded. Sign rules (for example "a deposit is positive") belong to each context. Equality is numeric: `10.5 EUR == 10.50 EUR`.
- **Arithmetic** in one currency: `+`, `−`, unary `−`, comparisons and `*` by a `decimal` factor. Multiplication **does not round**. There is **no division operator**, because `a / 3` cannot be exact.
- **Currency mismatch**: an operator or `CompareTo` on two currencies throws `InvalidOperationException`, because inside a decision it is a bug. At an input boundary, `TryAdd` and `TrySubtract` return `shared.currency-mismatch` (Validation) instead. `decimal` overflow throws `OverflowException`.
- **Rounding** is always explicit and goes to the currency's minor units. `Round()` uses `MidpointRounding.ToEven` (banker's rounding, no upward bias over many postings). `Round(mode)` names another mode where a business rule asks for it, for example a fee rounded `AwayFromZero`. `IsRoundedToMinorUnits` tells whether rounding would change the amount.
- **Allocation**: `Allocate(ratios)` and `Split(parts)` divide a **rounded** amount into whole minor units with the **largest-remainder method**. Each part gets its exact share truncated towards zero. The left-over minor units (fewer than the number of parts) go one each to the parts with the largest remainder, and to the first of them on a tie. So the parts always add up to exactly the original amount, keep its sign, and each is within one minor unit of its exact share: `10.00 EUR`.`Split(3)` gives `3.34`, `3.33`, `3.33`. The calculation uses `BigInteger` and is exact. An unrounded amount throws `InvalidOperationException` (round it first); empty or non-positive ratios throw `ArgumentException`. A first draft gave the left-over units to the first parts, which can move a part a full minor unit away from an exact share; a property test now pins the rule.
- **Untrusted input**: `Money.TryCreate(string? amount, string? currencyCode)` accepts only an optional leading `-`, digits, and optionally a dot followed by **at most `MinorUnits` digits**. Whitespace, grouping separators, exponents, `+`, `.5` and `5.` are refused, and so is `10.500` for EUR (it is either valid or rejected, never silently rounded). The currency is checked first. Error messages never echo the input.
- **Formatting**: invariant culture, at least `MinorUnits` decimals, no grouping, never `-0`. `FormatAmount()` gives `"10.50"` and `ToString()` gives `"10.50 EUR"`.
- **JSON**: `{ "amount": "10.50", "currency": "EUR" }`. The amount is a **string**, as BIAN's `AmountValue` is, so no JSON client reads it as a float. Reading accepts the properties in any order, ignores unknown properties (tolerant reader) and **keeps any scale**, because stored events and views may hold unrounded amounts; the syntax is as strict as `TryCreate`. Client input is mapped through `TryCreate` in `.Api` / `.Mcp`, which also enforces minor units. `Currency` and `Money` carry `[JsonConverter]` attributes, so Marten, Wolverine and ASP.NET Core need no registration (0.10, 0.11, 1.5).
- **`default`**: `default(Currency)` has no code and `default(Money)` has no currency; neither can be prevented for a struct (the same limitation as in ADR 0008). `Money.Of` refuses `default(Currency)`, every operation on `default(Money)` throws `InvalidOperationException`, writing either as JSON throws `JsonException`, and `TryCreate` never returns them.

### Error codes

| Code | Kind | When |
|---|---|---|
| `shared.invalid-currency` | Validation | Not a currency in the table; field `currency` from `Money.TryCreate` |
| `shared.invalid-amount` | Validation | Malformed amount or more decimals than the currency allows; field `amount` |
| `shared.currency-mismatch` | Validation | `TryAdd` / `TrySubtract` with two currencies |

They become 400 Problem Details in 0.9.

### Testing

- **FsCheck 3.6.0** with `FsCheck.Xunit.v3` 3.6.0. `[Property]` and `[Properties(Arbitrary = …)]` are discovered and run by xUnit v3 4.0.2 on Microsoft.Testing.Platform (ADR 0007), and a failing property reports a shrunk counter-example with its replay seed.
- `tests/RimBank.SharedKernel.Testing/MoneyArbitraries.cs` provides `Arbitrary<Currency>` (0, 2 and 3 minor units equally likely), `Arbitrary<Money>` (rounded or with up to four extra decimals), and `RoundedMoney`, `SameCurrencyPair` and `SameCurrencyTriple`, with shrinking towards zero and fewer decimals. Amounts stay below 10^12 units, so the arithmetic laws hold exactly. Later property tests (3.3, 6.2) reuse them.

## Usage sample

Kept compiling as `tests/RimBank.SharedKernel.Tests/MoneyAndCurrency/MoneyUsageSampleTests.cs`:

```csharp
// Boundary (.Api / .Mcp): untrusted text becomes Money or a Validation error (400 from 0.9).
private static Result<Money> ParseAmount(string? amount, string? currency) => Money.TryCreate(amount, currency);

// Decision: a fee is a named rate, rounded explicitly with the mode the product terms name.
private static Money Fee(Money amount, decimal rate) => (amount * rate).Round(MidpointRounding.AwayFromZero);

// Decision: two amounts from different requests are combined only when their currencies match.
private static Result<Money> Total(Money first, Money second) => first.TryAdd(second);

var fee = from deposit in ParseAmount("1234.50", "EUR") select Fee(deposit, 0.015m); // 18.52 EUR
var shares = Money.TryCreate("10.00", "EUR").Value.Split(3);                         // 3.34, 3.33, 3.33
```

## Consequences

- Contexts never handle a bare `decimal` amount: commands, events and views carry `Money`, and adapters map BIAN's `AmountValue` / `AmountCurrency` with `Money.TryCreate`.
- Every rounding is visible at its call site with its mode, so a reviewer can check it against the product terms or the BIAN rule.
- Dividing money always goes through `Allocate` / `Split`, so postings and fee splits can always be reconciled (6.4).
- The ISO 4217 table is a reviewed snapshot; RimBank does not follow new editions automatically.
- Exchange rates, conversion and percentage types are out of scope; the phase that first needs them adds them with its own ADR.
- Follow-up for 0.7: an ArchUnitNET rule that no field, property or parameter in cores, SharedKernel or `.PublishedLanguage` is `double` or `float`.

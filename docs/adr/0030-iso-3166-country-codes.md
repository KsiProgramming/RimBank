# 0030. ISO 3166 country codes in SharedKernel

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.18

## Context

Roadmap 1.18 records the party's `Nationality` and `PartyCountryOfResidence` in the Reference Behavior Qualifier ([ADR 0029](0029-reference-fields-in-stages.md)). Each is a BIAN `Country` object, whose only part with business meaning is `Code`: "a nation with its own government (ISO 3166)". Its ISO 20022 type is `CountryCode`, the two-letter alpha-2 code.

RimBank has no country value object yet. More contexts will need one soon:
- 1.19 (ISO 20022 postal addresses);
- Payments (5.x: debtor and creditor parties, ISO 20022);
- later, regulatory reporting.

[Mission](../../specs/mission.md) principle 3 rules out a bare string. [Tech-stack](../../specs/tech-stack.md) §7 puts value objects that every context shares in `RimBank.SharedKernel`, which depends on the BCL only. `Currency` ([ADR 0009](0009-money-and-currency.md)) already shows how a closed ISO code list is modelled there.

## Options considered

1. **`CountryCode` in SharedKernel, built like `Currency`.**
   - Pros: one definition for every context; the ISO list is reviewed once; it fits the BCL-only rule.
   - Cons: SharedKernel grows by one type, chosen before its second user exists.
2. **A country value object in the Party core (`Reference/`), moved to SharedKernel when 1.19 or Payments needs it.**
   - Pros: the smallest shared surface today.
   - Cons: a move within weeks, which changes a stored type's namespace and every reference to it.
3. **A third-party package (for example an ISO 3166 NuGet).**
   - Pros: no list to maintain.
   - Cons: SharedKernel would depend on more than the BCL (tech-stack §4), and the package decides when codes change.
4. **Alpha-3 or numeric codes.**
   - Pros: none for RimBank.
   - Cons: ISO 20022 `CountryCode` and BIAN use alpha-2.

## Decision

Option 1. `RimBank.SharedKernel.CountryCode` is a `readonly record struct` with:
- the alpha-2 `Code`;
- the ISO 3166-1 `NumericCode`;
- `All`;
- `TryCreate` (returns `Result`, error `shared.invalid-country`);
- `From` (trusted input; throws on a programmer error).

- **The table.**
  - It holds the **249 officially assigned** alpha-2 codes, embedded in `CountryCode.Iso3166.cs`.
  - User-assigned codes (`AA`, `QM`–`QZ`, `XA`–`XZ`, `ZZ`, and so `XK`) are left out, as are exceptionally reserved codes (`EU`, `UK`, …) and transitionally reserved ones (`AN`, …).
  - `CountryCodeTableSnapshotTests` pins the table against the reviewed `Iso3166.snapshot.csv`, so any change shows up in review.
- **Exact codes.**
  - As for `Currency`, `TryCreate` accepts only the exact upper-case code, with no trimming.
  - A caller that accepts looser input normalises it first. The Party core trims and upper-cases what a client sends (`fr` → `FR`), so the leniency is a decision of the context that receives the input, not of the shared type.
- **JSON.** A plain string such as `"FR"`, also as a dictionary key, through `CountryCodeJsonConverter` applied by attribute (no registration).

## Consequences

- 1.19 and Payments reuse `CountryCode` and do not define their own.
- A change of ISO 3166 (a new or withdrawn code) is a reviewed change to the table and its snapshot.
- BIAN's `Country` object stays an adapter concern. The core records a `CountryCode`, and the HTTP adapter maps it to `{ "Code": "FR" }`.
- Stored events and views hold the plain code, so a later change of the type's internals does not touch stored data.

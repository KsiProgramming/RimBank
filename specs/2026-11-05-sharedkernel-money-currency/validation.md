# Validation: SharedKernel: Money & Currency

## Done when (from roadmap)
> **0.5 SharedKernel: Money & Currency:** ISO 4217 currency, minor units, rounding policy, with arithmetic property tests (FsCheck).
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: the "working code", "unit tests", "CI green", "ADR" and "roadmap tick" items apply. There are no adapters, read models, BIAN operations or HTTP/MCP endpoints yet, so the projection, integration, Problem Details and MCP parity items do not apply. The three `Validation` errors added here get their Problem Details mapping in 0.9. Architecture tests start in 0.7. As the user chose, the proof is **FsCheck money laws + ISO 4217 table tests + reusable arbitraries + a green CI run + ADR 0009 with a compiling sample**.

## Automated checks
- [ ] `tests/RimBank.SharedKernel.Tests` is green (xUnit v3 + Shouldly + FsCheck on MTP), with:
  - [ ] **FsCheck money laws** (at least 100 cases each): `+` commutative and associative, `Zero` identity, `a − a = Zero`, `a + b − b = a`, `−(−a) = a`, comparison agrees with amounts, mixed currencies never combine
  - [ ] **Rounding properties**: `Round` is idempotent, within half a minor unit, and its result is `IsRoundedToMinorUnits`; examples prove `ToEven` is the default and `AwayFromZero` works when named
  - [ ] **Allocation properties**: parts sum exactly to the original (no minor unit created or lost), all rounded, same currency, each within one minor unit of its exact share; examples for `Split(3)`, ratios, `JPY`, `BHD`, negative and zero amounts
  - [ ] **ISO 4217 table tests**: spot checks (`EUR` 2, `JPY` 0, `KRW` 0, `BHD` 3, `KWD` 3, `TND` 3, `EUR` numeric 978); `null`, empty, lower-case, padded, unknown, `XAU`, `XDR`, `XXX`, `CLF`, `XAD` rejected with `Validation` `shared.invalid-currency`; `XAF`, `XOF`, `XCD` accepted; the table snapshot test matches (155 codes, SIX edition 2026-09-17); no fund or "N.A." codes; minor units only 0/2/3
  - [ ] **Parsing**: invariant, dot-only, rejects grouping, exponent, whitespace and too many decimals with `shared.invalid-amount` (field `amount`); bad code gives field `currency`
  - [ ] **Formatting and JSON**: culture-independent (tested under `fr-FR`); `TryCreate(FormatAmount(), Code)` and JSON round-trip properties; JSON shape `{ "amount": "10.50", "currency": "EUR" }`; invalid JSON throws `JsonException`
  - [ ] Mixed-currency operators throw `InvalidOperationException`; `TryAdd`/`TrySubtract` return `shared.currency-mismatch`
- [ ] **Reusable arbitraries**: `MoneyArbitraries` in `tests/RimBank.SharedKernel.Testing` provides `Arbitrary<Currency>`, `Arbitrary<Money>` and same-currency pair/triple generators, and the SharedKernel property tests use them (not local copies)
- [ ] The ADR 0009 usage sample exists as `MoneyUsageSampleTests` and compiles
- [ ] CI workflow `ci`, job `build`, green on the PR; the `test-results` artifact contains the TRX file with the property tests
- [ ] `dotnet format RimBank.slnx --verify-no-changes` passes
- [ ] Release build with `TreatWarningsAsErrors=true` and `latest-recommended` analysers: 0 warnings
- [ ] CodeQL `analyze (csharp)` green with 0 error-level findings
- [ ] `RimBank.SharedKernel.csproj` still has no `PackageReference` or `ProjectReference`; no `double` or `float` in `src/RimBank.SharedKernel` (code review plus `grep -rnwE "double|float" src/RimBank.SharedKernel` → no hits; ArchUnitNET from 0.7)

## Manual / demo checks
- [ ] From the repo root, the CONTRIBUTING commands give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0, `failed: 0`
- [ ] Break a law on purpose (for example, give the allocation remainder to no part), run the tests, and check that FsCheck reports a failing, shrunk counter-example; then revert
- [ ] `Money.TryCreate("10.505", "EUR")` returns a `Validation` failure with code `shared.invalid-amount`, field `amount` and a business-language message; `Money.TryCreate("10.50", "eur")` fails with `shared.invalid-currency`, field `currency` (shown by tests; they become 400 Problem Details from 0.9)
- [ ] Read ADR 0009: a reviewer can tell from it which codes are accepted, how to round, how to divide, and what the JSON looks like, without reading the code

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.5 ticked in the same PR, and the 0.7 follow-up rule (no `double`/`float`) added
- [ ] ADR 0009 (Money & Currency) written, status Accepted, with the ISO 4217 source edition and the FsCheck answer
- [ ] Both open questions in requirements.md answered (FsCheck xUnit v3 integration, `Directory.Packages.props` encoding)
- [ ] Only FsCheck packages added to `Directory.Packages.props`; no versions in `.csproj` files
- [ ] No business-specific types in SharedKernel (no account, fee or interest concepts)
- [ ] `CONTRIBUTING.md` updated with the Money note

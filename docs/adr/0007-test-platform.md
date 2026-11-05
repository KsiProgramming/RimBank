# 0007. Test platform: xUnit v3 on Microsoft.Testing.Platform

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.3

## Context

`specs/tech-stack.md` §9 chooses xUnit v3 and Shouldly for unit tests. Phase 0.3 adds the first test project, so it has to settle how `dotnet test` runs. ADR 0005 left this open: the CI step used VSTest's `--logger trx`.

With the .NET 10 SDK, `dotnet test` runs tests either through the older VSTest host or through **Microsoft.Testing.Platform (MTP)**, chosen for the whole repository in `global.json`. xUnit v3 test projects are executables with MTP built in, and the xUnit v3 templates default to MTP. The two modes take different command-line arguments.

## Options considered

1. **VSTest.** Keeps `--logger trx`, but needs the extra `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` packages and runs on the host the SDK is moving away from.
2. **Microsoft.Testing.Platform.** Enabled with one `global.json` setting, needs no adapter packages, and is what xUnit v3 is built for. The test executables can also be run directly.

## Decision

Option 2.

- `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`.
- Test projects are `OutputType=Exe` and reference only `xunit.v3` and `Shouldly` (versions in `Directory.Packages.props`). Projects that need them add Testcontainers, Alba, FsCheck or ArchUnitNET in their own phase.
- CI and local runs use:

  ```bash
  dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults
  ```

  `--report-xunit-trx` is xUnit v3's built-in TRX reporter, so no MTP TRX extension package is needed. The TRX file in `TestResults/` is uploaded as the `test-results` artifact.
- Test method names may use underscores (`Bind_passes_a_failure_through`): CA1707 is off for `tests/**` in `.editorconfig`.

## Consequences

- Contributors need the .NET 10 SDK pinned in `global.json`; older SDKs ignore the `test.runner` setting.
- VSTest-only options (`--logger`, `--collect`, `.runsettings` data collectors) no longer apply. Coverage, if added later, uses an MTP extension (for example `Microsoft.Testing.Extensions.CodeCoverage`).
- Filters use MTP/xUnit syntax, for example `dotnet test --solution RimBank.slnx --filter-class RimBank.SharedKernel.Tests.ErrorTests`.
- ADR 0005's note about `--logger trx` is superseded by this ADR for the test step only; the rest of 0005 stands.

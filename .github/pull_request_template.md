## Roadmap phase

<!-- e.g. "0.3 SharedKernel: Result & Error", or "n/a" for a fix outside the roadmap -->
Phase:
Planning package: `specs/YYYY-MM-DD-<slug>/`

## Summary

<!-- What changed and why, in business language. -->

## Definition of done

- [ ] Working code that follows [tech-stack.md](../specs/tech-stack.md) (naming, dependency rules, business-only names in core)
- [ ] Unit tests for domain and application logic, plus integration tests (Testcontainers) for any adapter touched
- [ ] Architecture tests green
- [ ] Every new error path returns RFC 9457 Problem Details (on both HTTP and MCP)
- [ ] Every new BIAN operation exposed over HTTP is also exposed as an MCP tool, with a parity test
- [ ] GitHub Actions CI green (build, test, format, CodeQL)
- [ ] An ADR in `docs/adr/` for any significant or BIAN-interpretation decision
- [ ] The roadmap checkbox ticked in the same PR

## Validation

<!-- Checks from validation.md you ran, demo requests or MCP tool calls, and their results. Mark items that do not apply as N/A with a reason. -->

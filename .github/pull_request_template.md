## Roadmap phase

<!-- e.g. "0.3 SharedKernel: Result & Error", or "n/a" for a fix outside the roadmap -->
Phase:
Planning package: `specs/YYYY-MM-DD-<slug>/`

## Summary

<!-- What changed and why, in business language. -->

## Definition of done

- [ ] Working code that follows [tech-stack.md](../specs/tech-stack.md) (naming, dependency rules, business-only names in core)
- [ ] Commands and queries kept separate as described in [tech-stack.md §8](../specs/tech-stack.md#8-commands-queries--read-models-cqrs): commands go through decisions and return acknowledgements, queries read only from read models
- [ ] Unit tests for domain and application logic, including a projection test (Given events → Then view) for every new or changed read model, plus integration tests (Testcontainers) for any adapter touched
- [ ] Every new or changed read model can be rebuilt from the context's own event store
- [ ] Contexts stay loosely coupled as described in [tech-stack.md §8.4](../specs/tech-stack.md#84-read-models-across-contexts-loose-coupling): no shared or cross-context read models, and no cross-context references except `.Subscriptions` → `.PublishedLanguage`
- [ ] Architecture tests green
- [ ] Every new error path returns RFC 9457 Problem Details (on both HTTP and MCP)
- [ ] Every new BIAN operation exposed over HTTP is also exposed as an MCP tool, with a parity test
- [ ] GitHub Actions CI green (build, test, format, CodeQL)
- [ ] An ADR in `docs/adr/` for any significant or BIAN-interpretation decision
- [ ] Architecture and business documentation (`docs/architecture/`, `docs/business/`) updated for the change, with every Mermaid diagram rendering (the `update-docs` skill)
- [ ] The roadmap checkbox ticked in the same PR

## Validation

<!-- Checks from validation.md you ran, demo requests or MCP tool calls, and their results. Mark items that do not apply as N/A with a reason. -->

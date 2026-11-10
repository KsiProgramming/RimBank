# RimBank

[![ci](https://github.com/KsiProgramming/RimBank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KsiProgramming/RimBank/actions/workflows/ci.yml)
[![codeql](https://github.com/KsiProgramming/RimBank/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/KsiProgramming/RimBank/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/KsiProgramming/RimBank/badge)](https://scorecard.dev/viewer/?uri=github.com/KsiProgramming/RimBank)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](global.json)
[![BIAN v14](https://img.shields.io/badge/BIAN-v14-0a7d6e.svg)](doc-0/release14.0.0/)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

**RimBank is an open-source, production-grade core banking platform built in .NET that implements the [BIAN](https://bian.org) Service Landscape v14 using Domain-Driven Design and hexagonal architecture.**

The code is meant to read like the business it serves. Every BIAN service domain becomes a bounded context, and its names, flows and capabilities come from BIAN and banking, not from frameworks. Every capability is available both as a BIAN HTTP API and as an [MCP](https://modelcontextprotocol.io) tool for AI agents, over the same core and with the same business rules.

> **Status:** early development. Stage 0 (Foundations) and Stage 1 (Party Reference Data Directory) are done; Stage 2 (Product Directory) is next. See the [roadmap](specs/roadmap.md).
> RimBank is not a licensed bank and provides no regulatory certification.

## Highlights

- **BIAN v14, faithfully:** service domains, Control Records, Behavior Qualifiers and operations mapped one-to-one, checked by a BIAN conformance harness against the official OpenAPI and AsyncAPI files.
- **Event-sourced DDD core:** pure decisions, append-only events in PostgreSQL (Marten), read models rebuilt from each context's own event store, and optimistic concurrency and idempotency keys on every command.
- **HTTP and MCP, the same rules:** every BIAN operation is a REST endpoint *and* a [Model Context Protocol](https://modelcontextprotocol.io) tool for AI agents, with parity tests, per-operation OAuth scopes and RFC 9457 Problem Details on both.
- **Modular monolith, loosely coupled:** contexts integrate only through published integration events (Wolverine outbox), and architecture tests enforce the boundaries.
- **Production habits from day one:** .NET Aspire, OpenTelemetry, Testcontainers, warnings as errors, locked restore, SHA-pinned CI, CodeQL and OpenSSF Scorecard.

## How BIAN maps to the code

| BIAN concept | DDD / code concept | Example |
|---|---|---|
| Service domain | Bounded context (a folder and namespace of the one `RimBank.Core` project, plus its own adapters) | `RimBank.Party.PartyReferenceDataDirectory` |
| Control Record | Aggregate root (event-sourced) | `PartyReferenceDataDirectoryEntry` |
| Behavior Qualifier | Entity inside the aggregate (or its own aggregate, by ADR) | `Demographics`, `Reference` |
| Operation (Register, Initiate, Update, Control, Execute, …) | Command with a pure decision, appending events | `RegisterParty` |
| Retrieve operation | Query over a read model (projection of events) | `RetrievePartyReferenceDataDirectoryEntry` |
| AsyncAPI channel | Integration event in `.PublishedLanguage` | `PartyReferenceDataDirectoryEntryCreated` |

Namespaces follow `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` (adapters: `.Api`, `.Mcp`, `.Postgresql`, `.Subscriptions`, …). Contexts never share read models; they integrate only through published integration events (see [ADR 0003](docs/adr/0003-separate-commands-and-queries.md) and [ADR 0004](docs/adr/0004-loosely-coupled-contexts.md)).

## Repository layout

```
├─ RimBank.slnx               # solution
├─ global.json                # pinned .NET SDK
├─ Directory.Build.props      # shared build settings (nullable, warnings as errors, analyzers)
├─ Directory.Packages.props   # central package versions
├─ specs/                     # the constitution: mission, tech stack, roadmap, phase plans
├─ docs/adr/                  # architecture decision records
├─ doc-0/release14.0.0/       # BIAN v14 specifications (read-only input, Apache 2.0, see THIRD-PARTY-NOTICES.md)
├─ src/                       # production code: RimBank.Core (every context's domain and application logic) and each context's adapters
└─ tests/                     # test projects
```

## Getting started

Prerequisite: the .NET SDK version pinned in [global.json](global.json) (.NET 10).

```bash
dotnet build RimBank.slnx
dotnet test RimBank.slnx
dotnet format RimBank.slnx --verify-no-changes
```

To run RimBank (Docker required): `dotnet run --project src/RimBank.AppHost`, then open the Aspire dashboard URL printed in the console. See [CONTRIBUTING](CONTRIBUTING.md#running-rimbank-locally).

## Documentation

- [Mission](specs/mission.md): why RimBank exists, guiding principles, quality bar, non-goals
- [Tech stack and architecture rules](specs/tech-stack.md)
- [Roadmap](specs/roadmap.md) and its Definition of done
- [Architecture and business documentation](docs/README.md): C4, data model, sequences, flows, dependencies, security; BIAN positioning, party glossary, relationships, value streams, use cases
- [Architecture decision records](docs/adr/)
- [BIAN v14 specifications](doc-0/release14.0.0/)

## Contributing

Contributions are welcome, from a typo fix to a whole BIAN service domain. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md), then pick a [good first issue](https://github.com/KsiProgramming/RimBank/labels/good%20first%20issue) or a phase from the [roadmap](specs/roadmap.md).

- Questions and ideas: [Discussions](https://github.com/KsiProgramming/RimBank/discussions). See [SUPPORT.md](SUPPORT.md).
- Who decides and how: [GOVERNANCE.md](GOVERNANCE.md).
- What changed: [CHANGELOG.md](CHANGELOG.md).
- Vulnerabilities: report them privately, as described in [SECURITY.md](SECURITY.md).

If RimBank helps you learn BIAN, DDD or event sourcing, a ⭐ helps others find it.

## License

[MIT](LICENSE) © 2026 RimBank contributors. The BIAN specifications in `doc-0/` are © BIAN under the Apache License 2.0. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). RimBank is not affiliated with BIAN. To cite RimBank, see [CITATION.cff](CITATION.cff).

# RimBank

[![ci](https://github.com/KsiProgramming/RimBank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KsiProgramming/RimBank/actions/workflows/ci.yml)
[![codeql](https://github.com/KsiProgramming/RimBank/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/KsiProgramming/RimBank/actions/workflows/codeql.yml)

**RimBank is an open-source, production-grade core banking platform built in .NET that implements the [BIAN](https://bian.org) Service Landscape v14 using Domain-Driven Design and hexagonal architecture.**

The code is meant to read like the business it serves. Every BIAN service domain becomes a bounded context, and its names, flows and capabilities come from BIAN and banking, not from frameworks. Every capability is available both as a BIAN HTTP API and as an [MCP](https://modelcontextprotocol.io) tool for AI agents, over the same core and with the same business rules.

> **Status:** early development. Stage 0 (Foundations) and Stage 1 (Party Reference Data Directory) are done; Stage 2 (Product Directory) is next. See the [roadmap](specs/roadmap.md).
> RimBank is not a licensed bank and provides no regulatory certification.

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
├─ doc-0/release14.0.0/       # BIAN v14 specifications (read-only input)
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

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 RimBank contributors

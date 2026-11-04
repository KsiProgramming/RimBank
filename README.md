# RimBank

**RimBank is an open-source, production-grade core banking platform built in .NET that implements the [BIAN](https://bian.org) Service Landscape v14 using Domain-Driven Design and hexagonal architecture.**

The code is meant to read like the business it serves. Every BIAN service domain becomes a bounded context, and its names, flows and capabilities come from BIAN and banking, not from frameworks. Every capability is available both as a BIAN HTTP API and as an [MCP](https://modelcontextprotocol.io) tool for AI agents, over the same core and with the same business rules.

> **Status:** early development, Stage 0 (Foundations). See the [roadmap](specs/roadmap.md).
> RimBank is not a licensed bank and provides no regulatory certification.

## How BIAN maps to the code

| BIAN concept | DDD / code concept | Example |
|---|---|---|
| Service domain | Bounded context (one core project) | `RimBank.Party.PartyReferenceDataDirectory` |
| Control Record | Aggregate root (event-sourced) | `PartyReferenceDataDirectoryEntry` |
| Behavior Qualifier | Entity inside the aggregate (or its own aggregate, by ADR) | `Demographics`, `Reference` |
| Operation (Initiate, Update, Retrieve, …) | Command or query, with a pure decision | `RegisterParty` |
| AsyncAPI channel | Integration event in `.PublishedLanguage` | `PartyReferenceDataDirectoryEntryCreated` |

Namespaces follow `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` (adapters: `.Api`, `.Mcp`, `.Postgresql`, …).

## Repository layout

```
├─ RimBank.slnx               # solution
├─ global.json                # pinned .NET SDK
├─ Directory.Build.props      # shared build settings (nullable, warnings as errors, analyzers)
├─ Directory.Packages.props   # central package versions
├─ specs/                     # the constitution: mission, tech stack, roadmap, phase plans
├─ docs/adr/                  # architecture decision records
├─ doc-0/release14.0.0/       # BIAN v14 specifications (read-only input)
├─ src/                       # production code
└─ tests/                     # test projects
```

## Getting started

Prerequisite: the .NET SDK version pinned in [global.json](global.json) (.NET 10).

```bash
dotnet build RimBank.slnx
dotnet test RimBank.slnx
dotnet format RimBank.slnx --verify-no-changes
```

## Documentation

- [Mission](specs/mission.md): why RimBank exists, guiding principles, quality bar, non-goals
- [Tech stack and architecture rules](specs/tech-stack.md)
- [Roadmap](specs/roadmap.md) and its Definition of done
- [Architecture decision records](docs/adr/)
- [BIAN v14 specifications](doc-0/release14.0.0/)

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 RimBank contributors

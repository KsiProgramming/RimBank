# RimBank documentation

Two views of the same platform. Start with the one that matches your question; each links to the other.

| If you want to know… | Read |
|---|---|
| What RimBank does for a bank, where it fits in BIAN, what each party field means, which value streams it serves | [Business documentation](business/README.md) |
| How it is built: C4, data model, sequences, logic flows, data flow, dependencies, security, operations, risks | [Architecture documentation](#architecture-documentation) |
| Why a specific decision was made | [Architecture decision records](adr/) |
| The rules every change must follow | [Mission](../specs/mission.md), [tech stack](../specs/tech-stack.md), [roadmap](../specs/roadmap.md) |

## Business documentation

| # | Document | For |
|---|---|---|
| 1 | [Business architecture](business/01-business-architecture.md) | BIAN positioning, capability map, adoption patterns |
| 2 | [Party domain glossary](business/02-party-domain-glossary.md) | Every entity member, enumeration, event and error, with ISO / BIAN definitions |
| 3 | [Entity relationships](business/03-entity-relationships.md) | Business ERD, cardinalities, worked examples |
| 4 | [Value streams](business/04-value-streams.md) | End-to-end value delivery and which service domain serves each stage |
| 5 | [Banking use cases and integration guide](business/05-banking-use-cases.md) | KYC, PEP, beneficial ownership, FATCA/CRS, servicing, AI agents; consuming Party events |

## Architecture documentation

| # | Document | For |
|---|---|---|
| 1 | [Architecture overview](architecture/01-architecture-overview.md) | Drivers, quality priorities, styles, key decisions, BIAN → architecture mapping |
| 2 | [C4 model](architecture/02-c4-model.md) | System context, containers, components, code |
| 3 | [Data model and ERD](architecture/03-data-model-erd.md) | Logical ERD, event model, physical PostgreSQL model, published language, read models |
| 4 | [Sequence diagrams](architecture/04-sequence-diagrams.md) | Register, idempotent replay, Retrieve/ETag, Update/concurrency, cross-aggregate checks, associations, verification, MCP, errors, cross-context (target) |
| 5 | [Logic flows and state machines](architecture/05-logic-flows.md) | Request pipeline, every decision tree, lifecycles |
| 6 | [Data flow](architecture/06-data-flow.md) | DFD levels 0–1, CQRS paths, cross-context data, classification, lineage |
| 7 | [Dependencies](architecture/07-dependencies.md) | Project graph, dependency matrix, hexagon, packages, runtime, context map |
| 8 | [Deployment, security and operations](architecture/08-deployment-security-operations.md) | Aspire topology, reference production deployment, trust boundaries, STRIDE, observability, CI/CD |
| 9 | [Quality attributes, risks and roadmap](architecture/09-quality-risks-roadmap.md) | Quality scenarios with evidence, risks, deferrals, readiness for Stage 2 |

## Conventions

- Diagrams are [Mermaid](https://mermaid.js.org) in the Markdown, so they render on GitHub and change in the same pull request as the code.
- Every document states whether it describes `main` today or a *(target)* from the roadmap.
- When a document and the code or an ADR disagree, the code and the ADR win: fix the document in the same pull request.
- A phase that changes a diagrammed behaviour (a new context, operation, event, table or rule) updates the affected document as part of its definition of done.

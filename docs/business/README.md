# RimBank business documentation

For bank business analysts, product owners, enterprise and solution architects, and new contributors who need to understand **what** RimBank does for a bank before reading **how** it is built (see [`docs/architecture/`](../architecture/)).

| # | Document | Answers |
|---|---|---|
| 1 | [Business architecture](01-business-architecture.md) | Where RimBank sits in the BIAN v14 landscape; the retail-core capability chain with BIAN definitions and status; system of record vs consumer; adoption patterns (greenfield, strangler, party MDM, AI-agent channel) |
| 2 | [Party domain glossary](02-party-domain-glossary.md) | Every member of the Party Reference Data Directory Entry and its four Behavior Qualifiers: business definition, ISO / BIAN format, rules, business use, PII class; every enumeration; business events; error codes |
| 3 | [Entity relationships](03-entity-relationships.md) | The business ERD; cardinalities and rules; Associations vs corporate references vs bank relations; worked examples (corporate group, household, servicing model) |
| 4 | [Value streams](04-value-streams.md) | Onboard a party, open a product, move money, record and report, service the relationship: stages, service domains, status, KPIs; value stream × service domain matrix |
| 5 | [Banking use cases and integration guide](05-banking-use-cases.md) | KYC/CDD capture, PEP declaration, beneficial ownership, FATCA/CRS inputs, servicing assignment, contact data, ID&V document references, AI assistant via MCP; how consumers subscribe to Party events |

## Status conventions

- **Implemented** — in `main`, tested, on HTTP and MCP (Stage 1, roadmap 1.1–1.21).
- **Planned** — a numbered item of the [roadmap](../../specs/roadmap.md) (Stages 2–7).
- **Backlog** — Stage 8+, order not decided.
- **Deferred** — a BIAN operation or channel of an implemented service domain that is not built yet.
- **Not recorded** — a BIAN field deliberately not kept, by ADR.

## Sources of truth

1. BIAN v14 ISO 20022 extended DDD specifications: [`doc-0/release14.0.0/apis-iso20022_ext-ddd/`](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/) (quoted verbatim in these documents).
2. The RimBank constitution: [mission](../../specs/mission.md), [tech stack](../../specs/tech-stack.md), [roadmap](../../specs/roadmap.md).
3. Architecture decision records: [`docs/adr/`](../adr/), in particular [0024](../adr/0024-party-reference-data-directory-mapping.md) (Party mapping), [0028](../adr/0028-demographics-exchange-as-verification.md)–[0035](../adr/0035-party-bank-relations.md) (Behavior Qualifier fields) and [0036](../adr/0036-party-published-language.md) (published events).

When these documents and the code disagree, the code and the ADRs win; please open an issue.

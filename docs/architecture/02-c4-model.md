# 02. C4 model

The [C4 model](https://c4model.com) at four levels. Diagrams marked *(target)* include contexts from the roadmap that do not exist yet; all others describe `main` today.

## Level 1: System context

Who uses RimBank and what it relies on.

```mermaid
C4Context
    title System context: RimBank core banking platform

    Person(staff, "Bank staff", "Onboarding, operations and relationship managers using the bank's own front ends")
    Person(customer, "Bank customer", "Uses the bank's digital channels")

    System_Ext(channels, "Bank channel applications", "Web, mobile, branch and back-office apps built by the bank")
    System_Ext(agents, "AI agents", "Assistants and automations speaking the Model Context Protocol")
    System(rimbank, "RimBank", "BIAN v14 core banking platform: party, product, account, position, payment and accounting capabilities")
    System_Ext(idp, "Identity provider", "Keycloak or any OIDC / OAuth 2.1 authorization server")
    System_Ext(consumers, "Downstream bank systems", "CRM, KYC/AML, data warehouse, regulatory reporting: consume integration events")
    System_Ext(rails, "Payment rails (target)", "SEPA / ISO 20022 pacs schemes behind ports, Stage 8+")
    System_Ext(obs, "Observability backend", "Any OTLP-compatible tracing, metrics and logging stack")

    Rel(customer, channels, "Uses")
    Rel(staff, channels, "Uses")
    Rel(staff, agents, "Asks")
    Rel(channels, rimbank, "BIAN HTTP APIs", "HTTPS, JSON, JWT")
    Rel(agents, rimbank, "MCP tools", "Streamable HTTP, JWT")
    Rel(rimbank, idp, "Validates tokens, advertises authorization server", "OIDC")
    Rel(channels, idp, "Obtains tokens", "OAuth 2.1")
    Rel(rimbank, consumers, "Publishes integration events (target: RabbitMQ)", "AsyncAPI channels")
    Rel(rimbank, rails, "Sends and receives payments (target)")
    Rel(rimbank, obs, "Traces, metrics, logs", "OTLP")
```

**Key point:** RimBank has no end-user UI ([mission non-goals](../../specs/mission.md#non-goals)). Every human reaches it through a bank-built channel or an AI agent. Both channel types are equal citizens: same core, same policies, same errors.

## Level 2: Containers

```mermaid
C4Container
    title Containers: RimBank modular monolith (today)

    System_Ext(channels, "Bank channel applications", "BIAN HTTP clients")
    System_Ext(agents, "AI agents", "MCP clients")
    System_Ext(idp, "Keycloak", "Realm rimbank, audience rimbank-api")

    System_Boundary(rb, "RimBank") {
        Container(host, "RimBank.Host", ".NET 10, ASP.NET Core, Wolverine, Marten", "One deployable process: BIAN HTTP endpoints, /mcp endpoint, message bus, every context's core and adapters")
        ContainerDb(pg, "PostgreSQL database rimbank", "PostgreSQL", "Schema per context: event streams, inline views, outbox, inbox, idempotency outcomes. Schema wolverine for node bookkeeping")
        Container(apphost, "RimBank.AppHost", ".NET Aspire", "Local orchestration: PostgreSQL, Keycloak, per-context role connection strings, dashboard")
    }

    System_Ext(otel, "OTLP backend", "Aspire dashboard locally")

    Rel(channels, host, "Register, Update, Retrieve …", "HTTPS /PartyReferenceDataDirectory/…")
    Rel(agents, host, "tools/list, tools/call", "Streamable HTTP /mcp")
    Rel(host, pg, "Reads and writes, one login role per context", "Npgsql")
    Rel(host, idp, "JWKS, discovery", "HTTPS")
    Rel(host, otel, "Telemetry", "OTLP")
    Rel(apphost, host, "Starts, injects configuration")
    Rel(apphost, pg, "Starts")
    Rel(apphost, idp, "Starts with realm import")
```

| Container | Responsibility | Technology | Scales by |
|---|---|---|---|
| `RimBank.Host` | All request handling, decisions, persistence, messaging | ASP.NET Core Minimal APIs, MCP C# SDK, Wolverine, Marten | Horizontal instances; Wolverine `DurabilityMode.Balanced` shares durable work across nodes |
| PostgreSQL | System of record: events, views, outbox/inbox, idempotency | PostgreSQL | Vertical first; read replicas or dedicated read stores only by ADR (roadmap 7.6) |
| Keycloak | Tokens, scopes, MCP authorization server | Keycloak (dev); any OIDC provider elsewhere | External |
| `RimBank.AppHost` | Developer and test orchestration | .NET Aspire | Not deployed to production |

### Container view after extraction *(target)*

The modular-monolith promise ([tech-stack §2](../../specs/tech-stack.md#2-topology-modular-monolith-first)): extracting a context moves its core folder and adapters into a new service and swaps the local durable queue for RabbitMQ. No core code changes.

```mermaid
flowchart LR
    subgraph H1["RimBank.Host"]
        P["Party context"]
        PR["Product Directory context"]
    end
    subgraph H2["CurrentAccount service (extracted)"]
        CA["Current Account context"]
    end
    MQ[["RabbitMQ<br/>integration events"]]
    DB1[("schemas: party_reference_data_directory,<br/>product_directory")]
    DB2[("schema: current_account")]
    P -- "outbox → PartyReferenceDataDirectoryEntryCreated" --> MQ
    PR -- "outbox → product events" --> MQ
    MQ -- "inbox → .Subscriptions" --> CA
    H1 --> DB1
    H2 --> DB2
```

## Level 3: Components of `RimBank.Host` for the Party context

```mermaid
C4Component
    title Components: Party Reference Data Directory inside RimBank.Host

    Container_Boundary(host, "RimBank.Host") {
        Component(auth, "Security", "JWT bearer, OperationPolicies, ActorRelay", "Authenticates, authorises per BIAN operation id, relays the caller subject to the bus")
        Component(pd, "Problem Details", "RimBank.Problems + host handlers", "Maps Error → RFC 9457, unhandled exceptions → 500 shared.unexpected")
        Component(module, "PartyReferenceDataDirectoryModule", "IServiceDomainModule", "Registers the context: store, ports, endpoints, MCP tools, policies, scopes")

        Component(api, "Party .Api folder", "RimBank.Api / Party / PartyReferenceDataDirectory", "13 BIAN endpoints: request/response models, Idempotency-Key, ETag/If-Match, dispatch")
        Component(mcp, "Party .Mcp folder", "RimBank.Mcp / Party / PartyReferenceDataDirectory", "13 MCP tools: arguments, results, isError Problem Details, same policies")

        Component(bus, "Wolverine bus", "IMessageBus", "InvokeAsync dispatch, idempotency and concurrency-retry policies, transactional outbox")

        Component(cmd, "Command handlers", "RimBank.Core", "RegisterParty, Update*, Exchange*, Register/UpdatePartyAssociation: load → Decide → append → acknowledge → publish")
        Component(qry, "Query handlers", "RimBank.Core", "Retrieve*: read the view through the read port")
        Component(dom, "Aggregates and decisions", "RimBank.Core", "PartyReferenceDataDirectoryEntry, PartyAssociation: pure Decide / Evolve")
        Component(proj, "Projections", "RimBank.Core", "Pure view evolve: entry view, association view")
        Component(pub, "Publication mapper", "RimBank.Core", "Domain events → published language events")

        Component(wp, "MartenPartyDirectory", ".Postgresql", "IPartyDirectory write port over event streams")
        Component(rp, "MartenPartyDirectoryEnquiry", ".Postgresql", "IPartyDirectoryEnquiry read port over inline views")
        Component(pl, "PublishedLanguage", ".PublishedLanguage", "PartyReferenceDataDirectoryEntryCreated / Updated v1")
    }

    ContainerDb(pg, "PostgreSQL", "schema party_reference_data_directory")

    Rel(auth, api, "Authorised requests")
    Rel(auth, mcp, "Authorised tool calls")
    Rel(api, bus, "InvokeAsync(command | query)")
    Rel(mcp, bus, "InvokeAsync(command | query)")
    Rel(bus, cmd, "Dispatches commands")
    Rel(bus, qry, "Dispatches queries")
    Rel(cmd, dom, "Decide / Evolve")
    Rel(cmd, wp, "Load, start, append")
    Rel(cmd, pub, "Map decided events")
    Rel(pub, pl, "Creates")
    Rel(qry, rp, "Find view")
    Rel(wp, pg, "Event streams")
    Rel(rp, pg, "View documents")
    Rel(proj, pg, "Run inline by Marten on append")
    Rel(api, pd, "error.ToHttpResult()")
    Rel(mcp, pd, "error.ToToolResult()")
```

### Component catalogue

| Component | Project / folder | Role (hexagonal) | Key types |
|---|---|---|---|
| Security | `RimBank.Host/Security` | Infrastructure (edge) | `AuthenticationComposition`, `OperationPolicies`, `OperationScopeRequirement`, `ActorRelay` |
| Module | `RimBank.Host/Modules/Party` | Composition | `PartyReferenceDataDirectoryModule` (scopes `party-reference-data-directory:read` / `:write`) |
| HTTP adapter | `RimBank.Api/Party/PartyReferenceDataDirectory/*` | Driving adapter | `RegisterPartyEndpoint`, `Update…Endpoint`, `Retrieve…Endpoint`, request/response records |
| HTTP conventions | `RimBank.Api` (root) | Driving adapter support | `IdempotencyKeyHeader`, `RequireIdempotencyKey()`, ETag / If-Match helpers |
| MCP adapter | `RimBank.Mcp/Party/PartyReferenceDataDirectory/*` | Driving adapter | 13 tools `party_reference_data_directory_*` |
| MCP conventions | `RimBank.Mcp` (root) | Driving adapter support | `ToToolResult`, tool error filter, authorisation filter, `[ToolOperation]` |
| Error mapping | `RimBank.Problems` | Shared edge | `Error` → Problem Details (`type`, `errorCode`, `traceId`, `errors`) |
| Core | `RimBank.Core/Party/PartyReferenceDataDirectory` | Domain + application | aggregates, commands, queries, handlers, views, projections, ports, publication |
| Persistence | `src/Party/…Postgresql` | Driven adapter | `MartenPartyDirectory`, `MartenPartyDirectoryEnquiry`, `PartyReferenceDataDirectoryPostgresql.Configure` |
| Persistence conventions | `RimBank.Postgresql` | Driven adapter support | `AddServiceDomainStore`, `IdempotencyGate`, `CommandConcurrencyRetryPolicy`, `AddInlineView` |
| Published language | `src/Party/…PublishedLanguage` | Contract | `PartyReferenceDataDirectoryEntryCreated`, `…Updated` |
| Shared kernel | `RimBank.SharedKernel` | Domain primitives | `Result`, `Error`, `ITypedId`, `Money`, `Currency`, `CountryCode`, `IDecider`, `IProjection`, `ICommand`, `IQuery`, `Actor` |

## Level 4: Code (domain model of the Party core)

```mermaid
classDiagram
    direction LR

    class IDecider~TState,TCommand,TEvent~ {
        <<interface>>
        +Decide(command, state) Result~events~
        +Evolve(state, event) state
    }

    class PartyReferenceDataDirectoryEntry {
        <<aggregate root / Control Record>>
        +PartyReferenceDataDirectoryEntryId? Id
        +PartyType? PartyType
        +DateTimeOffset? OpenDate
        +DateTimeOffset? RefreshDate
        +DemographicsId? DemographicsId
        +PartyReferenceId? PartyReferenceId
        +BankRelationsId? BankRelationsId
        +DemographicsRecord? Demographics
        +PartyReferenceDetails? Reference
        +BankRelationsDetails? BankRelations
        +Decide()$
        +Evolve()$
    }

    class DemographicsRecord {
        <<entity: Demographics BQ>>
        +PartyDemographics Details
        +DemographicsVerificationStatus VerificationStatus
        +DateTimeOffset? VerificationDate
    }
    class PartyDemographics {
        <<value object>>
        +string? SocioEconomicClassification
        +Employment? Employment
        +string? EmploymentHistory
        +string? EducationHistory
        +string? ServicingConstraints
    }

    class PartyReferenceDetails {
        <<entity: Reference BQ>>
        +PartyIdentifiers Identifiers
        +PartyNames? Names
        +DateOnly? DateOfBirth
        +PartyResidency? Residency
        +PartyAddresses? Addresses
        +PartyContactPoints? ContactPoints
        +PartyLegalStanding? LegalStanding
        +PartyDocuments? Documents
        +PartyCorporateReferences? CorporateReferences
    }

    class BankRelationsDetails {
        <<entity: BankRelations BQ>>
        +BankRelationType? Type
        +EntryId? BusinessUnitEmployee
        +DateTimeOffset? StartDate
        +DateTimeOffset? EndDate
        +BankRelationLifecycleStatus? LifecycleStatus
    }

    class PartyAssociation {
        <<aggregate root: Associations BQ>>
        +PartyAssociationId? Id
        +EntryId? OwnerId
        +EntryId? AssociateId
        +PartyRelationshipType? AssociateType
        +AssociationValidityPeriod? ValidityPeriod
        +Decide()$
        +Evolve()$
    }

    class IPartyDirectory {
        <<write port>>
        +LoadEntryAsync()
        +StartEntry()
        +AppendToEntry(expectedVersion)
        +LoadAssociationAsync()
        +StartAssociation()
        +AppendToAssociation(expectedVersion)
    }
    class IPartyDirectoryEnquiry {
        <<read port>>
        +FindEntryAsync()
        +FindAssociationAsync()
    }

    IDecider <|.. PartyReferenceDataDirectoryEntry
    IDecider <|.. PartyAssociation
    PartyReferenceDataDirectoryEntry *-- "0..1" DemographicsRecord
    DemographicsRecord *-- PartyDemographics
    PartyReferenceDataDirectoryEntry *-- "0..1" PartyReferenceDetails
    PartyReferenceDataDirectoryEntry *-- "0..1" BankRelationsDetails
    PartyAssociation --> "1 owner" PartyReferenceDataDirectoryEntry : by id
    PartyAssociation --> "1 associate" PartyReferenceDataDirectoryEntry : by id
    PartyReferenceDetails ..> PartyReferenceDataDirectoryEntry : corporate and officer refs by id
    BankRelationsDetails ..> PartyReferenceDataDirectoryEntry : employee or unit by id
    IPartyDirectory ..> PartyReferenceDataDirectoryEntry : event stream
    IPartyDirectory ..> PartyAssociation : event stream
```

Notes on level 4:

- Aggregates reference each other **only by id**. The handler loads the referenced entry on the write side to check it exists and has the right party type before `Decide` runs ([ADR 0034](../adr/0034-reference-corporate-and-officer-references.md), [ADR 0035](../adr/0035-party-bank-relations.md)).
- `EntryId` in the diagram abbreviates `PartyReferenceDataDirectoryEntryId` (a Guid v7 `readonly record struct`, [ADR 0008](../adr/0008-typed-identifier-pattern.md)).
- The full member dictionary with business definitions is in [../business/02-party-domain-glossary.md](../business/02-party-domain-glossary.md).

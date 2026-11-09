# 04. Sequence diagrams

Each diagram shows the real components and the order of checks. Participants:

| Short name | Component |
|---|---|
| Client | A BIAN HTTP client (bank channel) |
| Agent | An MCP client (AI agent) |
| Auth | ASP.NET Core authentication + `OperationPolicies` + `ActorRelay` (`RimBank.Host/Security`) |
| Endpoint | The context's endpoint in `RimBank.Api` |
| Tool | The context's tool in `RimBank.Mcp` |
| Bus | Wolverine `IMessageBus` with the idempotency and concurrency-retry policies (`RimBank.Postgresql`) |
| Handler | Command or query handler in `RimBank.Core` |
| Decider | The aggregate's pure `Decide` / `Evolve` |
| WritePort / ReadPort | `MartenPartyDirectory` / `MartenPartyDirectoryEnquiry` |
| PG | PostgreSQL schema `party_reference_data_directory` |

## 1. Register a party (command, happy path)

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Auth
    participant Endpoint as RegisterPartyEndpoint
    participant Bus as Wolverine bus
    participant Gate as IdempotencyGate
    participant Handler as RegisterPartyHandler
    participant Decider as PartyReferenceDataDirectoryEntry
    participant WritePort as MartenPartyDirectory
    participant PG as PostgreSQL

    Client->>Auth: POST /PartyReferenceDataDirectory/Register<br/>Authorization: Bearer JWT, Idempotency-Key: K<br/>{"PartyType":"Person"}
    Auth->>Auth: validate JWT (issuer, audience rimbank-api)
    Auth->>Auth: policy "PartyReferenceDataDirectoryEntry/Register"<br/>requires scope party-reference-data-directory:write
    Auth->>Auth: ActorRelay: sub → message context UserName
    Auth->>Endpoint: authorised request
    Endpoint->>Endpoint: RequireIdempotencyKey filter, map body → RegisterParty (PartyTypes.FromBianName)
    Endpoint->>Bus: InvokeAsync<Result<Ack>>(RegisterParty)
    Bus->>PG: BEGIN
    Bus->>Gate: EnterAsync(command, caller, key)
    Gate->>PG: pg_advisory_xact_lock(key), find IdempotentOutcome
    PG-->>Gate: none
    Gate-->>Bus: Proceed
    Bus->>Handler: HandleAsync(command, directory, time)
    Handler->>Handler: new EntryId, DemographicsId, PartyReferenceId, BankRelationsId (Guid v7)<br/>OpenDate = time.GetUtcNow()
    Handler->>WritePort: LoadEntryAsync(EntryId)
    WritePort->>PG: read stream
    PG-->>WritePort: []
    Handler->>Decider: Decide(RegisterPartyReferenceDataDirectoryEntry, Initial)
    Decider-->>Handler: Ok([PartyRegistered])
    Handler->>WritePort: StartEntry(EntryId, events)
    Handler->>Handler: Evolve → acknowledgement (version 1)<br/>Publication.Created(...) → integration event
    Handler-->>Bus: (Ok(Ack), PartyReferenceDataDirectoryEntryCreated)
    Bus->>Gate: Remember(ticket, Ack)
    Bus->>PG: append PartyRegistered, inline view upsert,<br/>IdempotentOutcome insert, outbox envelope
    Bus->>PG: COMMIT
    Bus-->>Endpoint: Ok(Ack)
    Endpoint-->>Client: 200 OK, ETag: "1"<br/>{ ids, PartyType, OpenDate }
    Note over Bus,PG: After commit, the durability agent delivers the outbox envelope<br/>to the context's durable local queue (target: RabbitMQ)
```

Key properties: one database transaction holds events, view, idempotency outcome and outbox, so they are **all or nothing**. The response is built from the acknowledgement, never from the view.

## 2. Same command retried (idempotent replay) and misuse

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Bus as Wolverine bus
    participant Gate as IdempotencyGate
    participant Handler
    participant PG as PostgreSQL

    Client->>Bus: RegisterParty, key K, same payload (e.g. network retry)
    Bus->>Gate: EnterAsync
    Gate->>PG: lock K, find outcome
    PG-->>Gate: outcome, same fingerprint
    Gate-->>Bus: replay stored acknowledgement
    Bus-->>Client: 200, identical body and ETag (handler NOT run, no new events)

    Client->>Bus: RegisterParty, key K, different payload
    Bus->>Gate: EnterAsync
    Gate->>PG: find outcome
    PG-->>Gate: outcome, different fingerprint
    Gate-->>Client: 409 shared.idempotency-key-reused

    par two concurrent requests with key K
        Client->>Bus: request A
        Bus->>Gate: lock K acquired
    and
        Client->>Bus: request B
        Bus->>Gate: lock K busy
        Gate-->>Client: 409 shared.idempotency-key-in-use
    end
```

Keys are scoped to *context + command type + caller subject*: two callers using the same key never interfere ([ADR 0018](../adr/0018-idempotency-keys-for-commands.md), [ADR 0019](../adr/0019-authentication-baseline.md)). Only successes are stored, so a failed command can be retried with the same key.

## 3. Retrieve with ETag and conditional GET (query)

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Auth
    participant Endpoint as RetrievePartyReferenceDataDirectoryEntryEndpoint
    participant Bus
    participant Handler as RetrieveEntryHandler
    participant ReadPort as MartenPartyDirectoryEnquiry
    participant PG

    Client->>Auth: GET /PartyReferenceDataDirectory/{id}/Retrieve
    Auth->>Auth: policy …/Retrieve requires party-reference-data-directory:read
    Auth->>Endpoint: authorised
    Endpoint->>Endpoint: parse {id}: not a Guid v7 → 400 shared.invalid-identifier
    Endpoint->>Bus: InvokeAsync(RetrievePartyReferenceDataDirectoryEntry)
    Bus->>Handler: Handle(query, enquiry)
    Handler->>ReadPort: FindEntryAsync(id)
    ReadPort->>PG: SELECT view document
    alt view not found
        PG-->>Handler: null
        Handler-->>Endpoint: Error NotFound party.party-not-found
        Endpoint-->>Client: 404 application/problem+json
    else found
        PG-->>Handler: view (Version = n)
        Handler-->>Endpoint: Ok(view)
        alt If-None-Match matches "n"
            Endpoint-->>Client: 304 Not Modified, ETag "n"
        else
            Endpoint-->>Client: 200, ETag "n", BIAN response body
        end
    end
```

No idempotency, no transaction, no write port: the CQRS architecture rules forbid a query handler from touching aggregates or the write port.

## 4. Update with optimistic concurrency and lost-race retry

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as Update…Endpoint
    participant Bus
    participant Retry as CommandConcurrencyRetryPolicy
    participant Handler as Update…Handler
    participant Decider
    participant WritePort
    participant PG

    Client->>Endpoint: PUT …/{id}/Update, If-Match: "3", Idempotency-Key: K
    alt If-Match missing
        Endpoint-->>Client: 428 shared.if-match-required
    else weak, *, or list
        Endpoint-->>Client: 400 shared.if-match-invalid
    end
    Endpoint->>Bus: InvokeAsync(Update… ExpectedVersion=3)
    Bus->>Handler: attempt 1
    Handler->>WritePort: LoadEntryAsync(id)
    WritePort-->>Handler: 3 events (version 3)
    Handler->>Handler: version == ExpectedVersion ✔
    Handler->>Decider: Decide(update, state)
    Decider-->>Handler: Ok([…Updated])
    Handler->>WritePort: AppendToEntry(id, expectedVersion 3, events)
    Bus->>PG: COMMIT
    PG-->>Bus: ConcurrencyException (another writer committed version 4)
    Note over Bus,PG: nothing of attempt 1 is stored, idempotency lock released
    Bus->>Retry: retry once
    Retry->>Handler: attempt 2
    Handler->>WritePort: LoadEntryAsync(id)
    WritePort-->>Handler: version 4
    Handler-->>Bus: Error Conflict party.version-conflict (value, not exception)
    Bus-->>Endpoint: Error
    Endpoint-->>Client: 409 party.version-conflict → client re-Retrieves and retries with If-Match "4"
```

## 5. Update Reference with corporate references (cross-aggregate check on the write side)

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Handler as UpdatePartyReferenceHandler
    participant WritePort
    participant Decider as Entry.Decide
    participant PG

    Client->>Handler: UpdatePartyReference(entry O, CorporateCustomer=C, CompanyOfficer=P, Role="Director")
    Handler->>WritePort: LoadEntryAsync(O)
    Handler->>WritePort: LoadEntryAsync(C), LoadEntryAsync(P)
    WritePort->>PG: read streams O, C, P
    Handler->>Handler: ReferencedParties = {C: Organisation, P: Person}
    Handler->>Decider: Decide(UpdatePartyReferenceDetails{Changes, Today, ReferencedParties}, state O)
    alt O is not an Organisation
        Decider-->>Handler: party.corporate-references-need-organisation
    else C == O
        Decider-->>Handler: party.self-reference
    else C not registered
        Decider-->>Handler: party.referenced-party-not-registered
    else P is an Organisation
        Decider-->>Handler: party.referenced-party-type-mismatch
    else Role without officer
        Decider-->>Handler: party.reference-invalid (officer role without officer)
    else valid
        Decider-->>Handler: Ok([PartyReferenceUpdated(full Reference snapshot)])
        Handler->>WritePort: AppendToEntry(O, expectedVersion, events)
    end
```

Only stream **O** is appended. C and P are read for validation only; their later changes do not cascade (documented staleness, [ADR 0034](../adr/0034-reference-corporate-and-officer-references.md)).

## 6. Register a party association (own aggregate)

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as RegisterPartyAssociationEndpoint
    participant Handler as RegisterPartyAssociationHandler
    participant WritePort
    participant Decider as PartyAssociation.Decide
    participant PG

    Client->>Endpoint: POST /PartyReferenceDataDirectory/{owner}/Associations/Register<br/>{AssociateReference, AssociateType, AssociationValidFromToDate}
    Endpoint->>Endpoint: PartyAssociationTerms: refuse unrecorded BIAN fields,<br/>parse type (9 values), validity period
    Endpoint->>Handler: RegisterPartyAssociation
    Handler->>WritePort: LoadEntryAsync(owner), LoadEntryAsync(associate)
    Handler->>Decider: Decide(RegisterPartyAssociationDetails{OwnerIsRegistered, AssociateIsRegistered}, Initial)
    alt owner unknown
        Decider-->>Handler: party.party-not-found (404)
    else associate == owner
        Decider-->>Handler: party.self-association
    else associate unknown
        Decider-->>Handler: party.associate-not-registered
    else ok
        Decider-->>Handler: Ok([PartyAssociationRegistered])
        Handler->>WritePort: StartAssociation(newId, events)
        WritePort->>PG: new stream + inline PartyAssociationView
        Handler-->>Endpoint: PartyAssociationAcknowledged (id, version 1)
    end
```

## 7. Demographics verification (Exchange)

```mermaid
sequenceDiagram
    autonumber
    actor Officer as KYC officer (via channel)
    participant Handler as ExchangeDemographicsHandler
    participant Decider as Entry.Decide

    Officer->>Handler: ExchangeDemographics(Action = Verify | Reject), If-Match
    Handler->>Decider: Decide(ExchangeDemographicsDetails, state)
    alt demographics id not the entry's
        Decider-->>Handler: party.behavior-qualifier-not-found
    else nothing recorded yet
        Decider-->>Handler: party.demographics-not-recorded
    else
        Decider-->>Handler: DemographicsExchanged(Verified | Rejected, VerificationDate = now)
    end
    Note over Officer,Decider: Any later DemographicsUpdated that changes the details<br/>resets the status to Unverified (ADR 0028)
```

## 8. MCP tool call (AI agent), same core

```mermaid
sequenceDiagram
    autonumber
    actor Agent as AI agent
    participant MCP as /mcp (Streamable HTTP, stateless)
    participant Auth
    participant Filter as RimBank.Mcp filters
    participant Tool as party_reference_data_directory_update_demographics
    participant Bus
    participant Core as Core handler

    Agent->>MCP: POST /mcp (no token)
    MCP-->>Agent: 401 + WWW-Authenticate → RFC 9728 protected-resource metadata<br/>(authorization server = Keycloak realm, scopes_supported)
    Agent->>Agent: OAuth 2.1 with Keycloak → token (aud rimbank-api)
    Agent->>MCP: tools/list (Bearer)
    MCP->>Auth: authenticate
    MCP->>Filter: hide tools whose operation policy the caller fails
    MCP-->>Agent: tools the caller may use (read-only tools annotated readOnly)
    Agent->>MCP: tools/call {partyReferenceDataDirectoryId, demographicsId,<br/>idempotencyKey, expectedVersion, request:{…BIAN names…}}
    MCP->>Filter: authorise [ToolOperation("Demographics/Update")] → write scope
    alt arguments cannot bind
        Filter-->>Agent: isError, Problem Details shared.tool-arguments-invalid
    end
    Filter->>Tool: invoke
    Tool->>Bus: InvokeAsync(UpdateDemographics) – same command as HTTP
    Bus->>Core: idempotency, concurrency, decide, append
    Core-->>Tool: Result
    alt failure
        Tool-->>Agent: isError: true, structuredContent = same Problem Details as HTTP
    else success
        Tool-->>Agent: result = HTTP response body + version (HTTP's ETag)
    end
```

Parity tests assert that HTTP and MCP return the same JSON and the same Problem Details for the same operation ([ADR 0039](../adr/0039-party-mcp-tools.md)).

## 9. Error handling path (any request)

```mermaid
sequenceDiagram
    autonumber
    actor Caller
    participant Edge as Endpoint / Tool
    participant Problems as RimBank.Problems
    participant Host as Host exception handler

    Caller->>Edge: request
    alt business or validation failure (Result.Error)
        Edge->>Problems: error.ToHttpResult() / ToToolResult()
        Problems-->>Caller: problem+json {type: urn:rimbank:problem:party:…, title, status, detail, instance, errorCode, traceId, errors?}
    else unexpected exception
        Edge->>Host: throws
        Host-->>Caller: 500 shared.unexpected (no stack trace, traceId for support)
    else unknown route
        Host-->>Caller: 404 shared.not-found
    end
```

## 10. Planned: cross-context integration (Stage 4, target)

How Current Account will learn about parties without calling Party ([tech-stack §8.4](../../specs/tech-stack.md#84-read-models-across-contexts-loose-coupling)).

```mermaid
sequenceDiagram
    autonumber
    participant Party as Party handler
    participant Outbox as Party outbox (party schema)
    participant Queue as Durable queue / RabbitMQ
    participant Subs as CurrentAccount.Subscriptions
    participant CAStore as CurrentAccount store
    participant CA as InitiateFacility handler

    Party->>Outbox: PartyReferenceDataDirectoryEntryCreated.v1 (same txn as PartyRegistered)
    Outbox->>Queue: deliver (at-least-once)
    Queue->>Subs: message (inbox dedups by EventId)
    Subs->>Subs: translate to local "KnownPartyRecorded" (tolerant reader)
    Subs->>CAStore: append local reference event → "known party" view
    Note over CA,CAStore: Later, InitiateFacility checks the LOCAL known-party view.<br/>It never queries Party, staleness documented in an ADR.
    CA->>CAStore: read known-party view, decide, append
```

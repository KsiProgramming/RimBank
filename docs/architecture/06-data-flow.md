# 06. Data flow

Data-flow diagrams (DFD) in the Gane–Sarson spirit: external entities, processes, data stores and flows, at increasing depth. Then data classification and lineage.

## 1. DFD level 0 (context)

```mermaid
flowchart LR
    CH[/"Bank channels"/]
    AG[/"AI agents"/]
    IDP[/"Identity provider"/]
    DS[/"Downstream systems<br/>(CRM, KYC, DWH, reporting)"/]
    OB[/"Observability backend"/]

    RB(("0<br/>RimBank"))

    CH -- "commands: party data, ids, idempotency key, version" --> RB
    RB -- "acknowledgements, views, ETag, Problem Details" --> CH
    AG -- "tool calls (same commands and queries)" --> RB
    RB -- "tool results / isError Problem Details" --> AG
    IDP -- "signing keys, token claims: sub, azp, scope, aud" --> RB
    RB -- "integration events: no PII" --> DS
    RB -- "traces, metrics, logs (PII-masked, roadmap 7.2)" --> OB
```

## 2. DFD level 1 (inside RimBank, Party context)

```mermaid
flowchart TB
    CH[/"Channel / Agent"/]

    P1(("1.0<br/>Authenticate<br/>and authorise"))
    P2(("2.0<br/>Translate request<br/>(.Api / .Mcp)"))
    P3(("3.0<br/>Guard command<br/>(idempotency, version)"))
    P4(("4.0<br/>Decide<br/>(pure core)"))
    P5(("5.0<br/>Project views<br/>(inline)"))
    P6(("6.0<br/>Answer query"))
    P7(("7.0<br/>Publish integration<br/>events (outbox)"))

    D1[("D1 Event streams<br/>mt_events / mt_streams")]
    D2[("D2 Views<br/>entry, association")]
    D3[("D3 Idempotent outcomes")]
    D4[("D4 Outbox / inbox")]

    DS[/"Consumers"/]

    CH -- "request + JWT" --> P1
    P1 -- "authorised request + actor subject" --> P2
    P2 -- "command (typed values)" --> P3
    P2 -- "query (ids)" --> P6
    P3 -- "fingerprint, key" <--> D3
    P3 -- "command" --> P4
    D1 -- "stream history (own + referenced entries)" --> P4
    P4 -- "decided events" --> D1
    D1 -- "new events (same txn)" --> P5
    P5 -- "view documents" --> D2
    D2 -- "view" --> P6
    P6 -- "view + version" --> P2
    P4 -- "decided events" --> P7
    P7 -- "Created / Updated v1" --> D4
    D4 -- "after commit" --> DS
    P2 -- "acknowledgement / view / Problem Details" --> CH
```

**Invariant flows:**

- D1 is written **only** by process 4 through the write port. Nothing else writes events.
- D2 is written **only** by process 5. Queries (6) never read D1; commands (4) never read D2.
- D3, D1, D2 and D4 writes for a command share **one PostgreSQL transaction**.
- Process 7 maps from **decided events**, never from D2 ([ADR 0036](../adr/0036-party-published-language.md)).

## 3. CQRS data paths

```mermaid
flowchart LR
    subgraph Write["Write side (consistent)"]
        C[Command] --> H[Command handler]
        H --> WP[Write port]
        WP <--> ES[(Event stream)]
        H --> DEC[Decide]
    end
    subgraph Read["Read side (inline today, async when needed)"]
        PR[Projection] --> V[(View)]
        Q[Query] --> QH[Query handler] --> RP[Read port] --> V
    end
    ES -- events --> PR
    H -- ack built from decided state --> OUT1([Command response])
    QH -- view --> OUT2([Query response + ETag])
```

## 4. Cross-context data flow *(target, Stages 2–6)*

Each context owns its data; others receive only what they need, via events, and keep their own minimal copy ([tech-stack §8.4](../../specs/tech-stack.md#84-read-models-across-contexts-loose-coupling)).

```mermaid
flowchart LR
    PRDD["Party Reference<br/>Data Directory"]
    PD["Product Directory"]
    CA["Current Account"]
    PK["Position Keeping"]
    POI["Payment Order<br/>Initiation"]
    FA["Financial Accounting"]
    AR["Account<br/>Reconciliation"]
    CP["Customer Position<br/>(query-only)"]

    PRDD -- "EntryCreated/Updated: id, type" --> CA
    PD -- "product launched/withdrawn + terms" --> CA
    CA -- "facility initiated → open position log" --> PK
    POI -- "debit/credit/reversal requested" --> CA
    CA -- "payment/deposit executed or rejected" --> POI
    PK -- "FinancialTransactionCaptured" --> FA
    PK -- "totals at checkpoint" --> AR
    FA -- "GL totals at checkpoint" --> AR
    PRDD -. "events" .-> CP
    CA -. "events" .-> CP
    POI -. "events" .-> CP
```

| Data | Owner (system of record) | Copies allowed | Copy content |
|---|---|---|---|
| Party identity and type | Party Reference Data Directory | Current Account, Customer Position | Entry id, party type, "known" flag; **no PII** |
| Party demographics, identifiers, addresses, contacts | Party Reference Data Directory | None by default | Consumers that need them get them through future BQ channels, with an ADR |
| Product terms | Product Directory | Current Account | Currency, fees, interest terms needed for decisions |
| Authoritative balance | Decided in ADR 3.1 (write-side) | Views only | Projections never used for funds checks |
| GL postings | Financial Accounting | Reconciliation | Totals at checkpoints |

## 5. Data classification

| Class | Examples in Party | Where it exists | Controls today | Planned |
|---|---|---|---|---|
| **Sensitive personal data** | Tax id, national registration number, date of birth, government document number, political exposure | D1 (event payloads), D2 (views) | Per-context DB role, TLS, authenticated and scoped access, no PII in integration events | Field protection at rest in streams and views, log masking (roadmap 7.2) |
| **Personal data** | Full legal name, salutation, addresses, email, mobile, social network, nationality, residency, employment, education, servicing constraints | D1, D2 | Same | Same |
| **Business reference** | Party type, LEI, legal structure, relationships, bank relations, dates | D1, D2, events (type only) | Same | — |
| **Operational** | Idempotency fingerprints (SHA-256, never the payload), envelopes, traces | D3, D4, telemetry | Fingerprint only; trace carries `enduser.id` (subject) | Audit query API (7.3) |
| **Secrets** | DB role passwords, client secrets | User secrets locally; vault elsewhere | Never in source; Aspire generates 32-char role passwords | — |

### GDPR / privacy note

Event sourcing makes the audit trail immutable, which conflicts with erasure requests on personal data. The planned approach (roadmap 7.2) is protection of sensitive fields at rest so personal data can be made unreadable without rewriting history (e.g. per-subject key "crypto-shredding"). It needs its own ADR before Stage 7; until then **RimBank must not hold production personal data**.

## 6. Data lineage of a Retrieve response field

```mermaid
flowchart LR
    A["Channel sends Reference/Update<br/>TaxIdentificationNumber = ' FR123 '"] --> B["Adapter: PartyReferenceChanges<br/>trim, Max35Text check"]
    B --> C["Decide: PartyReferenceUpdated<br/>Details.Identifiers.TaxIdentificationNumber = 'FR123'"]
    C --> D[("mt_events: party_reference_updated<br/>data jsonb")]
    D --> E["PartyReferenceDataDirectoryEntryViewProjection"]
    E --> F[("view: Reference.Identifiers…")]
    F --> G["Reference/Retrieve response<br/>BIAN field name, ETag = stream version"]
```

Every value in a response can be traced back to one event in one stream, at one version, written by one actor at one time: that is the audit property the mission asks for.

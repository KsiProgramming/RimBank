# 05. Logic flows and state machines

Flowcharts of the request pipeline and of each decision, and state machines for every lifecycle in the Party context. Decision order matters: it defines *which* error a caller receives when several rules fail, and it is pinned by tests.

## 1. Request pipeline (HTTP command)

```mermaid
flowchart TD
    A([HTTP request]) --> B{Route known?}
    B -- no --> E404[404 shared.not-found]
    B -- yes --> C{Valid JWT?}
    C -- no --> E401[401 shared.unauthenticated]
    C -- yes --> C2{Actor identifiable<br/>sub and azp?}
    C2 -- no --> E401b[401 shared.actor-invalid]
    C2 -- yes --> D{Scope for this<br/>operation id?}
    D -- no --> E403[403 shared.forbidden]
    D -- yes --> F{Idempotency-Key<br/>present and valid?}
    F -- missing --> E400a[400 shared.idempotency-key-missing]
    F -- invalid --> E400b[400 shared.idempotency-key-invalid]
    F -- yes --> I{Route ids valid Guid v7?}
    I -- no --> E400d[400 shared.invalid-identifier]
    I -- yes --> G{Changes an existing record?}
    G -- yes --> H{If-Match?}
    H -- missing --> E428[428 shared.if-match-required]
    H -- weak, *, list --> E400c[400 shared.if-match-invalid]
    H -- strong tag --> J
    G -- no --> J{Body parses and<br/>maps to command?}
    J -- no --> E400e[400 validation Problem Details<br/>with errors per BIAN field]
    J -- yes --> K[[Bus: idempotency gate]]
    K -- replay --> OK200r[200 stored acknowledgement]
    K -- reused / in use --> E409a[409 shared.idempotency-key-*]
    K -- proceed --> L[[Handler: load, version check, Decide]]
    L -- Error --> M[Problems mapping → 4xx]
    L -- events --> N[(Commit: events + view + outcome + outbox)]
    N -- lost race --> L
    N -- ok --> OK200[200 acknowledgement + ETag]
```

Queries follow the same pipeline up to the scope check (read scope), then skip idempotency, `If-Match` and the transaction; they add the `If-None-Match` → 304 branch.

## 2. Entry decision (`PartyReferenceDataDirectoryEntry.Decide`)

The `switch` in `Decisions.cs`, as a decision tree. First matching rule wins.

```mermaid
flowchart TD
    S([command, state]) --> R{Command type}

    R -- Register --> R1{PartyType defined?}
    R1 -- no --> X1[party.party-type-invalid]
    R1 -- yes --> R2{Already registered?}
    R2 -- yes --> X2[party.party-already-registered]
    R2 -- no --> OK1[PartyRegistered]

    R -- Update entry --> U1{Registered?}
    U1 -- no --> X3[party.party-not-found]
    U1 -- yes --> U2{New type defined?}
    U2 -- no --> X1
    U2 -- yes --> U3{To Person while corporate<br/>references are recorded?}
    U3 -- yes --> X4[party.corporate-references-need-organisation]
    U3 -- no --> OK2[PartyReferenceDataDirectoryEntryUpdated<br/>type ?? current, RefreshDate]

    R -- BQ command --> B0{Registered?}
    B0 -- no --> X3
    B0 -- yes --> B1{BQ id is the entry's?}
    B1 -- no --> X5[party.behavior-qualifier-not-found]
    B1 -- yes --> BQ{Which BQ}
    BQ -- Demographics Update --> OK3[DemographicsUpdated<br/>status reset to Unverified if details changed]
    BQ -- Demographics Exchange --> D1{Action defined?}
    D1 -- no --> X6[party.exchange-action-type-invalid]
    D1 -- yes --> D2{Any details recorded?}
    D2 -- no --> X7[party.demographics-not-recorded]
    D2 -- yes --> OK4[DemographicsExchanged<br/>Verified or Rejected]
    BQ -- Reference Update --> RF[[Reference rules §3]]
    BQ -- BankRelations Update --> BR[[BankRelations rules §4]]
```

## 3. Reference update rules

```mermaid
flowchart TD
    A([UpdatePartyReferenceDetails]) --> B{DateOfBirth after today?}
    B -- yes --> X1[party.reference-invalid<br/>date of birth after today]
    B -- no --> C{CustomerSinceDate after today?}
    C -- yes --> X2[party.reference-invalid<br/>customer-since after today]
    C -- no --> D[Apply partial changes → updated details]
    D --> E{Corporate references present<br/>and party not Organisation?}
    E -- yes --> X3[party.corporate-references-need-organisation]
    E -- no --> F{For each referenced entry}
    F --> F1{Refers to itself?}
    F1 -- yes --> X4[party.self-reference]
    F1 -- no --> F2{Registered?}
    F2 -- no --> X5[party.referenced-party-not-registered]
    F2 -- yes --> F3{Expected type?<br/>officer = Person,<br/>customer and legal entity = Organisation}
    F3 -- no --> X6[party.referenced-party-type-mismatch]
    F3 -- yes --> G{PEP description without<br/>Foreign or Domestic exposure?}
    G -- yes --> X7[party.reference-invalid<br/>PEP description without exposure]
    G -- no --> H{CustomerSince before DateOfBirth?}
    H -- yes --> X8[party.reference-invalid<br/>customer-since before birth]
    H -- no --> I{Officer role without officer?}
    I -- yes --> X9[party.reference-invalid<br/>officer role without officer]
    I -- no --> OK[PartyReferenceUpdated<br/>full Reference snapshot]
```

Field-level validation (ISO 20022 lengths, LEI check digits, E.164, ISO 3166, address town and country required, document validity order) runs **before** the decision when the adapter builds `PartyReferenceChanges`, returning `party.reference-invalid` with per-field `errors`. Fields BIAN defines but RimBank does not yet record are refused with `party.reference-field-not-supported` rather than silently dropped ([ADR 0029](../adr/0029-reference-fields-in-stages.md)).

## 4. BankRelations update rules

```mermaid
flowchart TD
    A([UpdateBankRelationsDetails]) --> B[Apply partial changes]
    B --> C{Employee or unit reference<br/>without a relation type?}
    C -- yes --> X1[party.bank-relations-invalid<br/>reference without type]
    C -- no --> D{EndDate before StartDate?}
    D -- yes --> X2[party.bank-relations-invalid<br/>end before start]
    D -- no --> E{Reference to check?<br/>reference or type changed}
    E -- no --> OK
    E -- yes --> F{Refers to itself?}
    F -- yes --> X3[party.self-reference]
    F -- no --> G{Registered?}
    G -- no --> X4[party.referenced-party-not-registered]
    G -- yes --> H{Party type matches relation?<br/>RelationshipManager, AccountOfficer → Person<br/>ServicingBranch, ServicingBusinessUnit → Organisation}
    H -- no --> X5[party.referenced-party-type-mismatch]
    H -- yes --> OK[BankRelationsUpdated]
```

## 5. Association decision

```mermaid
flowchart TD
    A([command, association state]) --> T{Type}
    T -- Register --> R1{Association already registered?}
    R1 -- yes --> X1[party.association-already-registered]
    R1 -- no --> R2{AssociateType defined?}
    R2 -- no --> X2[party.association-invalid]
    R2 -- yes --> R3{Owner registered?}
    R3 -- no --> X3[party.party-not-found]
    R3 -- yes --> R4{Associate == owner?}
    R4 -- yes --> X4[party.self-association]
    R4 -- no --> R5{Associate registered?}
    R5 -- no --> X5[party.associate-not-registered]
    R5 -- yes --> OK1[PartyAssociationRegistered]
    T -- Update --> U1{Registered and owned<br/>by this entry?}
    U1 -- no --> X6[party.association-not-found]
    U1 -- yes --> U2{Type defined?}
    U2 -- no --> X2
    U2 -- yes --> U3{Tries to change the associate?}
    U3 -- yes --> X7[party.associate-cannot-change]
    U3 -- no --> OK2[PartyAssociationUpdated]
```

## 6. State machines

### 6.1 Directory entry lifecycle

```mermaid
stateDiagram-v2
    [*] --> Unregistered
    Unregistered --> Registered: Register / PartyRegistered
    Registered --> Registered: Update / …EntryUpdated (RefreshDate)
    Registered --> Registered: BQ Update or Exchange
    note right of Registered
        Control (suspend, terminate) is a deferred
        BIAN operation: no closed state yet.
        Lifecycle and KYC status belong to
        Party Lifecycle Management (Stage 8+).
    end note
```

### 6.2 Demographics verification ([ADR 0028](../adr/0028-demographics-exchange-as-verification.md))

```mermaid
stateDiagram-v2
    [*] --> Unverified: entry registered (no details)
    Unverified --> Unverified: Update
    Unverified --> Verified: Exchange Verify (details recorded)
    Unverified --> Rejected: Exchange Reject (details recorded)
    Verified --> Rejected: Exchange Reject
    Rejected --> Verified: Exchange Verify
    Verified --> Unverified: Update that changes details
    Rejected --> Unverified: Update that changes details
    Verified --> Verified: Update with identical details
```

### 6.3 Bank relation lifecycle ([ADR 0035](../adr/0035-party-bank-relations.md))

The status is **recorded**, not enforced as transitions: the bank declares it. The diagram shows the intended business meaning.

```mermaid
stateDiagram-v2
    [*] --> NotRecorded
    NotRecorded --> Active: Update (type, employee/unit, StartDate)
    Active --> Suspended
    Suspended --> Active
    Active --> Ended: EndDate set
    Suspended --> Ended
    Ended --> Active: re-assigned (new Update)
```

### 6.4 Party association

```mermaid
stateDiagram-v2
    [*] --> Registered: Associations/Register
    Registered --> Registered: Update type or validity period
    state Registered {
        [*] --> Pending: now before From
        Pending --> Effective: From reached
        Effective --> Expired: To passed
    }
    note right of Registered
        Effectiveness is derived from the validity
        period at read time, the associate never changes.
    end note
```

### 6.5 Command message (idempotency × concurrency)

```mermaid
stateDiagram-v2
    [*] --> Received
    Received --> Refused: key missing or invalid
    Received --> Replayed: same key, same fingerprint
    Received --> Refused: same key, other fingerprint or in use
    Received --> Deciding: new key, lock taken
    Deciding --> Rejected: Error (nothing stored, key reusable)
    Deciding --> Committing: events decided
    Committing --> Deciding: lost race (retry once)
    Committing --> Committed: events, view, outcome, outbox
    Committed --> Published: outbox delivered
    Replayed --> [*]
    Refused --> [*]
    Rejected --> [*]
    Published --> [*]
```

## 7. Read-model rebuild flow

```mermaid
flowchart LR
    A[Projection shape changes in a PR] --> B[Deploy new code]
    B --> C[RebuildViewAsync for the view]
    C --> D[(Drop view documents)]
    D --> E[Replay every stream of the context<br/>through the pure projection]
    E --> F[(Views rebuilt)]
    F --> G{GetViewProgressAsync<br/>caught up?}
    G -- no --> E
    G -- yes --> H([Serve queries])
```

The zero-downtime variant (blue/green view tables) is roadmap 7.4.

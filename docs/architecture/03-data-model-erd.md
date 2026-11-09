# 03. Data model and entity-relationship diagrams

RimBank is event-sourced, so there are three data models to understand, and they must not be confused:

| Model | What it is | Authoritative? | Where |
|---|---|---|---|
| **Domain model** (conceptual / logical) | Aggregates, entities and value objects in the core | Yes: decisions run against it | `RimBank.Core/Party/PartyReferenceDataDirectory` |
| **Event model** | The append-only streams of domain events that *are* the state | **Yes: the system of record** | Marten event tables in schema `party_reference_data_directory` |
| **Read model** | Views projected from events, shaped for queries | No: disposable, rebuildable at any time | Marten document tables in the same schema |

## 1. Logical domain ERD (Party Reference Data Directory)

Cardinalities are what the decisions enforce. Value objects are shown as attributes of their owning entity; [the business glossary](../business/02-party-domain-glossary.md) defines every member.

```mermaid
erDiagram
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY {
        guid EntryId PK "Guid v7, stream id"
        enum PartyType "Person | Organisation"
        datetime OpenDate "set at Register"
        datetime RefreshDate "set at each entry Update"
        guid DemographicsId UK "singleton BQ id"
        guid PartyReferenceId UK "singleton BQ id"
        guid BankRelationsId UK "singleton BQ id"
        long Version "stream version, ETag"
    }

    DEMOGRAPHICS {
        guid DemographicsId PK
        text SocioEconomicClassification "Max140Text"
        text EmploymentPartyReference "Max140Text, free text"
        text EmploymentInvolvementReference "Max140Text, free text"
        text EmploymentHistory "Max2048Text"
        text EducationHistory "Max2048Text"
        text ServicingConstraints "Max2048Text"
        enum VerificationStatus "Unverified | Verified | Rejected"
        datetime VerificationDate
    }

    REFERENCE {
        guid PartyReferenceId PK
        text TaxIdentificationNumber "Max35Text, PII"
        text NationalRegistrationNumber "Max35Text, PII"
        text LegalEntityIdentifier "ISO 17442 LEI"
        text FullLegalName "PII"
        text Salutation
        date DateOfBirth "not after today, PII"
        char2 Nationality "ISO 3166-1 alpha-2"
        char2 CountryOfResidence "ISO 3166-1 alpha-2"
        enum ResidencyStatusCode "Resident | NonResident"
        datetime ResidencyStatusDateTime
        enum LegalStructure "12 BIAN values"
        enum PoliticalExposureType "Foreign | Domestic | NoExposure"
        text PoliticalExposureDescription "Max350Text"
        date CustomerSinceDate "not after today, not before birth"
        text CompanyOfficerRole "Max35Text"
        guid CorporateCustomerEntryId FK "Organisation"
        guid LegalEntityEntryId FK "Organisation"
        guid CompanyOfficerEntryId FK "Person"
    }

    POSTAL_ADDRESS {
        enum Usage "Residential | Party | Corporate"
        text Department
        text SubDepartment
        text StreetName
        text StreetBuildingIdentification
        text BuildingName
        text Floor
        text PostOfficeBox
        text PostCodeIdentification
        text TownName "required"
        char2 Country "required, ISO 3166"
    }

    CONTACT_POINTS {
        text EmailAddress "PII"
        text MobileNumber "E.164, PII"
        text SocialNetworkAddress "https URL"
    }

    GOVERNMENT_ISSUED_DOCUMENT {
        enum PartyDocumentType "Work | Certification | Personal"
        text DocumentNumber "required, PII"
        text DocumentName
        date ValidFrom
        date ValidTo "not before ValidFrom"
        text DocumentDirectoryEntryReference "opaque ref to Document Directory"
    }

    BANK_RELATIONS {
        guid BankRelationsId PK
        enum Type "RelationshipManager | AccountOfficer | ServicingBranch | ServicingBusinessUnit"
        guid BusinessUnitEmployeeEntryId FK "Person or Organisation per Type"
        datetime StartDate
        datetime EndDate "not before StartDate"
        enum LifecycleStatus "Active | Suspended | Ended"
    }

    PARTY_ASSOCIATION {
        guid AssociationId PK "Guid v7, own stream"
        guid OwnerEntryId FK
        guid AssociateEntryId FK "immutable after Register"
        enum AssociateType "9 BIAN relationship types"
        datetime ValidFrom
        datetime ValidTo "not before ValidFrom"
        long Version
    }

    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY ||--|| DEMOGRAPHICS : "has exactly one (empty until first Update)"
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY ||--|| REFERENCE : "has exactly one"
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY ||--|| BANK_RELATIONS : "has exactly one"
    REFERENCE ||--o{ POSTAL_ADDRESS : "0..3, one per usage"
    REFERENCE ||--o| CONTACT_POINTS : "0..1"
    REFERENCE ||--o| GOVERNMENT_ISSUED_DOCUMENT : "0..1"
    REFERENCE }o--o| PARTY_REFERENCE_DATA_DIRECTORY_ENTRY : "corporate customer, legal entity, officer"
    BANK_RELATIONS }o--o| PARTY_REFERENCE_DATA_DIRECTORY_ENTRY : "employee or unit"
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY ||--o{ PARTY_ASSOCIATION : "owns"
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY ||--o{ PARTY_ASSOCIATION : "is associate in"
```

### Consistency boundaries

| Boundary | Contains | Transactional guarantee |
|---|---|---|
| Entry stream | Entry + Demographics + Reference + BankRelations | Every rule inside it is checked atomically against the current version |
| Association stream | One association | Atomic per association; many per party without contention on the party's stream ([ADR 0024](../adr/0024-party-reference-data-directory-mapping.md)) |
| Cross-aggregate references (FK lines above) | Ids of other entries | Checked on the **write side** at decision time (the handler loads the referenced entry). Not a database foreign key: a later change to the referenced entry does not cascade |

## 2. Event model

Every state change is one of these events. The first column is the Marten event type name stored in the database (`PartyReferenceDataDirectoryPostgresql.Configure`).

```mermaid
erDiagram
    ENTRY_STREAM ||--|{ ENTRY_EVENT : "ordered by version"
    ASSOCIATION_STREAM ||--|{ ASSOCIATION_EVENT : "ordered by version"
    ENTRY_EVENT {
        string party_registered "v1: EntryId, PartyType, OpenDate, DemographicsId, PartyReferenceId, BankRelationsId"
        string party_reference_data_directory_entry_updated "EntryId, PartyType, RefreshDate"
        string demographics_updated "EntryId, DemographicsId, Details, VerificationStatus, VerificationDate"
        string demographics_exchanged "EntryId, DemographicsId, VerificationStatus, VerificationDate"
        string party_reference_updated "EntryId, PartyReferenceId, Details (full snapshot of the BQ)"
        string party_bank_relations_updated "EntryId, BankRelationsId, Details (full snapshot)"
    }
    ASSOCIATION_EVENT {
        string party_association_registered "AssociationId, OwnerId, AssociateId, AssociateType, ValidityPeriod"
        string party_association_updated "AssociationId, AssociateType, ValidityPeriod"
    }
```

Design notes:

- **BQ events carry the full BQ state after the change** (not a delta). `Evolve` replaces the BQ; projections stay trivial; replays are deterministic.
- Event type names are **mapped explicitly** so that renaming a C# type never breaks stored history.
- Events are internal. Only `.PublishedLanguage` events leave the context (section 4).

## 3. Physical model (PostgreSQL)

One database (`rimbank`), one schema and one login role per context ([ADR 0016](../adr/0016-per-context-marten-stores.md)). Tables are created and migrated by Marten and Wolverine; names below are the libraries' conventions and may vary with their versions.

```mermaid
erDiagram
    MT_STREAMS ||--o{ MT_EVENTS : "stream_id"
    MT_STREAMS {
        uuid id PK "EntryId or AssociationId"
        string type "aggregate type"
        bigint version "current stream version"
        timestamptz created
        timestamptz timestamp
    }
    MT_EVENTS {
        bigint seq_id PK "global sequence in the schema"
        uuid id UK "event id"
        uuid stream_id FK
        int version "position in stream, unique with stream_id"
        string type "mapped event type name"
        jsonb data "event payload"
        timestamptz timestamp
    }
    MT_DOC_PARTYREFERENCEDATADIRECTORYENTRYVIEW {
        uuid id PK "EntryId"
        jsonb data "PartyReferenceDataDirectoryEntryView"
        timestamptz mt_last_modified
    }
    MT_DOC_PARTYASSOCIATIONVIEW {
        uuid id PK "AssociationId"
        jsonb data "PartyAssociationView"
        timestamptz mt_last_modified
    }
    MT_DOC_IDEMPOTENTOUTCOME {
        string id PK "commandType:callerLength:caller:key"
        string fingerprint "SHA-256 of payload without key"
        jsonb acknowledgement "first successful result"
        timestamptz mt_last_modified
    }
    WOLVERINE_OUTGOING_ENVELOPES {
        uuid id PK
        string message_type "party-reference-data-directory-entry.created.v1"
        bytea body
        string destination "local durable queue"
    }
    WOLVERINE_INCOMING_ENVELOPES {
        uuid id PK
        string status
        bytea body
    }
    MT_STREAMS ||--o| MT_DOC_PARTYREFERENCEDATADIRECTORYENTRYVIEW : "inline projection"
    MT_STREAMS ||--o| MT_DOC_PARTYASSOCIATIONVIEW : "inline projection"
```

| Object | Schema | Owner role | Written by | Read by |
|---|---|---|---|---|
| Event streams (`mt_events`, `mt_streams`) | `party_reference_data_directory` | `rimbank_party_reference_data_directory` | Write port (`AppendToEntry`, …) | Write port (load), projections |
| Entry and association views | same | same | Inline projections, in the append transaction | Read port only |
| Idempotent outcomes | same | same | `IdempotencyGate.Remember`, in the append transaction | `IdempotencyGate.EnterAsync` |
| Outbox / inbox envelopes | same | same | Wolverine, in the append transaction | Wolverine durability agent |
| Wolverine node bookkeeping | `wolverine` | `rimbank_wolverine` | Host | Host; **no context has grants** |

**Isolation proof:** an integration test proves one context's role cannot read another context's schema (roadmap 0.10). When Stage 2 adds `product_directory`, Party's role still cannot see it, and vice versa.

## 4. Published language (integration contract)

```mermaid
erDiagram
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY_CREATED_V1 {
        guid EventId PK "Guid v7, dedup key for consumers"
        datetime OccurredAt
        guid PartyReferenceDataDirectoryEntryId
        long Version "stream version after the event"
        string PartyType "Person | Organisation"
        string DirectoryEntryDateType "OpenDate"
        datetime DirectoryEntryDate
    }
    PARTY_REFERENCE_DATA_DIRECTORY_ENTRY_UPDATED_V1 {
        guid EventId PK
        datetime OccurredAt
        guid PartyReferenceDataDirectoryEntryId
        long Version
        string PartyType
        string DirectoryEntryDateType "RefreshDate"
        datetime DirectoryEntryDate
    }
```

| Channel (BIAN AsyncAPI) | Message identity | Emitted when |
|---|---|---|
| `PartyReferenceDataDirectoryEntry/Created` | `party-reference-data-directory-entry.created.v1` | `PartyRegistered` |
| `PartyReferenceDataDirectoryEntry/Updated` | `party-reference-data-directory-entry.updated.v1` | `PartyReferenceDataDirectoryEntryUpdated` |

The eight BQ channels (Demographics/Reference/Associations/BankRelations Created/Updated) are deferred ([ADR 0024](../adr/0024-party-reference-data-directory-mapping.md)). The contract deliberately carries **no PII**: it is enough for a consumer to know a party exists, its type and its version ([ADR 0036](../adr/0036-party-published-language.md)). A JSON-schema snapshot test pins each message so it can only evolve backwards-compatibly.

## 5. Read models

| View | Key | Lifecycle | Shape | Served operations |
|---|---|---|---|---|
| `PartyReferenceDataDirectoryEntryView` | EntryId | Inline | Entry header + version + Demographics + Reference + BankRelations | Entry, Demographics, Reference, BankRelations Retrieve |
| `PartyAssociationView` | AssociationId | Inline | Owner, associate, type, validity, version | Associations Retrieve |

Both are inline because callers read their own writes immediately (ETag from the command, then Retrieve). Cross-stream views (search, lists, "all associations of a party") are async projections when a phase needs them ([tech-stack §8.3](../../specs/tech-stack.md#83-projection-lifecycle-when-to-use-which)). Every view is rebuildable with `RebuildViewAsync`.

## 6. Shared kernel value types used in persistence

| Type | Representation | Standard |
|---|---|---|
| Typed ids (`…Id`) | `uuid`, Guid **v7** (time-ordered) | RFC 9562 |
| `CountryCode` | 2-letter upper-case string | ISO 3166-1 alpha-2 |
| `Currency` / `Money` (Stage 3+) | ISO code + `decimal` amount, minor-unit rules | ISO 4217 |
| Dates | `DateOnly` → `YYYY-MM-DD`; instants `DateTimeOffset` in UTC | ISO 8601 |
| Reference texts | Plain JSON strings, validated on read | ISO 20022 `MaxNText` |

# RimBank — Roadmap

> Part of the RimBank constitution. See also [mission.md](mission.md) and [tech-stack.md](tech-stack.md).
> This roadmap gives the high-level implementation order. Each phase is deliberately small: one pull request, about one focused day of work.

## Definition of done (every phase)

- [ ] Working code that follows [tech-stack.md](tech-stack.md) (naming, dependency rules, business-only names in core)
- [ ] Commands and queries kept separate as described in [tech-stack.md §8](tech-stack.md#8-commands-queries--read-models-cqrs): commands go through decisions and return acknowledgements, queries read only from read models
- [ ] Unit tests for domain and application logic, including a projection test (Given events → Then view) for every new or changed read model, plus integration tests (Testcontainers) for any adapter touched
- [ ] Every new or changed read model can be rebuilt from the context's own event store
- [ ] Contexts stay loosely coupled as described in [tech-stack.md §8.4](tech-stack.md#84-read-models-across-contexts-loose-coupling): no shared or cross-context read models, and no cross-context references except `.Subscriptions` → `.PublishedLanguage`
- [ ] Architecture tests green
- [ ] Every new error path returns RFC 9457 Problem Details (on both HTTP and MCP)
- [ ] Every new BIAN operation exposed over HTTP is also exposed as an MCP tool, with a parity test
- [ ] GitHub Actions CI green (build, test, format, CodeQL)
- [ ] An ADR in `docs/adr/` for any significant or BIAN-interpretation decision
- [ ] The roadmap checkbox ticked in the same PR

---

## Stage 0: Foundations

- [x] **0.1 Repository skeleton:** `RimBank.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, MIT `LICENSE`, `README.md`, `docs/adr/0001-record-architecture-decisions.md`.
- [x] **0.2 CI pipeline:** GitHub Actions workflow for restore, build, test and `dotnet format --verify-no-changes`; Dependabot; CodeQL.
- [x] **0.3 SharedKernel: Result & Error:** `Result<T>`, `Error` (code, message, kind, including `NotFound` and `Conflict`), with unit tests.
- [x] **0.4 SharedKernel: typed identifiers:** base pattern for `Guid` v7 strongly typed IDs.
- [x] **0.5 SharedKernel: Money & Currency:** ISO 4217 currency, minor units, rounding policy, with arithmetic property tests (FsCheck).
- [x] **0.6 SharedKernel: decider & projection abstractions:** write side `Decide` / `Evolve` contracts and read side `Project` (view, event) → view contract, with test helpers (Given events / When command / Then events, and Given events / Then view).
- [x] **0.7 Architecture tests:** ArchUnitNET project enforcing the dependency table, the CQRS rules (command handlers do not use query read models; query handlers do not use write ports or aggregates; projections are pure), cross-context isolation (only `.Subscriptions` may reference another context's `.PublishedLanguage`; `.PublishedLanguage` contains no views) forbidden folder names, every `ITypedId<T>` implementation being a `readonly record struct` (ADR 0008), and no `double` or `float` fields, properties or parameters in cores, SharedKernel or `.PublishedLanguage` (ADR 0009), and every test namespace ending with `.Tests` (tech-stack §3.3), and every aggregate state implementing `IDecider` and every `…Projection` implementing `IProjection` (ADR 0010). ADR on the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>` (ADR 0011). The CQRS roles are marked with `ICommand`, `IQuery<TView>`, `IWritePort` and `IReadPort` in SharedKernel (ADR 0012).
- [x] **0.8 Aspire AppHost + ServiceDefaults:** empty `RimBank.Host` with a health endpoint, OpenTelemetry, a PostgreSQL resource, and the module entry-point convention each context uses to register itself (`IServiceDomainModule`, ADR 0014).
- [x] **0.9 Problem Details baseline:** global exception handler, `Error` → Problem Details mapping convention, 404/500 tests asserting `application/problem+json`.
- [x] **0.10 Marten wiring:** one Marten store per context (own schema, own database role with no grants on other schemas, own async daemon); an inline projection and an async projection wired by convention; a projection rebuild entry point; Testcontainers smoke tests for append, inline read-your-write, async catch-up, and a test that one context's role cannot read another context's schema.
- [x] **0.11 Wolverine wiring:** command and query dispatch through `IMessageBus` (`InvokeAsync<T>`), local durable queues, transactional outbox with Marten, and a smoke test that sends a command, queries the result, and publishes and handles a message.
- [x] **0.12 Idempotency:** `Idempotency-Key` middleware or policy for commands, with tests (same key gives the same result, different payload gives 409). Queries are naturally idempotent and need no key.
- [x] **0.13 Authentication baseline:** Keycloak in Aspire, JWT bearer, deny by default, and a test that an unauthenticated request gets a 401 Problem Details response.
- [x] **0.14 BIAN conformance harness:** a test utility that loads an ext-DDD OpenAPI/AsyncAPI YAML from `doc-0/release14.0.0/` and asserts that the implemented routes and channels exist, and that `Retrieve` operations are `GET` (queries) and the rest are commands.
- [x] **0.15 MCP wiring:** `/mcp` Streamable HTTP endpoint in `RimBank.Host` using the MCP C# SDK, with a `ping`-style smoke tool and an in-process MCP client test. ADR on tool naming, annotations (query tools `readOnly`) and error mapping.
- [x] **0.16 MCP security & errors:** MCP authorization through Keycloak, the `Error` → Problem Details mapping as an `isError` tool result, and a test that an unauthenticated call is rejected.

## Stage 1: Party Reference Data Directory (first vertical slice)

`RimBank.Party.PartyReferenceDataDirectory`. Control Record: *PartyReferenceDataDirectoryEntry*. BQs: Demographics, Reference, Associations, BankRelations.

- [ ] **1.1 ADR:** BIAN → DDD mapping for this domain: aggregate boundaries, BQs as entities, the command/query split of its operations, and its read models (which views, inline or async).
- [ ] **1.2 Aggregate:** `PartyReferenceDataDirectoryEntry` state, `Register` decision, `PartyRegistered` event, invariants, unit tests.
- [ ] **1.3 Register flow (command side):** `RegisterParty` command and handler, `PartyDirectory` write port, in-memory test adapter.
- [ ] **1.4 PostgreSQL adapter (write side):** Marten event-stream implementation of `PartyDirectory`, with integration tests.
- [ ] **1.5 API adapter: Register:** `POST /PartyReferenceDataDirectory/Register` with validation returning Problem Details; the response is built from the command outcome, not from a read model.
- [ ] **1.6 Retrieve flow (query side):** `PartyReferenceDataDirectoryEntryView`, its pure projection, `RetrievePartyReferenceDataDirectoryEntry` query and handler, `PartyDirectoryEnquiry` read port and in-memory adapter, with projection unit tests.
- [ ] **1.7 Retrieve adapters:** inline Marten projection implementing `PartyDirectoryEnquiry`, and `GET /PartyReferenceDataDirectory/{id}/Retrieve` returning an `ETag` (404 returns Problem Details).
- [ ] **1.8 Update:** `Update` decision, event and command endpoint, with optimistic concurrency through `If-Match` (409 Problem Details); the view is updated by the projection.
- [ ] **1.9 Demographics BQ:** model, decisions, Update/Exchange commands, view fields and Retrieve query.
- [ ] **1.10 Reference BQ:** party identifiers (national ID, tax ID, LEI) as value objects; Update command and Retrieve query.
- [ ] **1.11 Associations BQ:** party-to-party relationships; Register/Update commands and Retrieve query.
- [ ] **1.12 BankRelations BQ:** the party's relationships with the bank; Update command and Retrieve query.
- [ ] **1.13 Published language:** `PartyReferenceDataDirectoryEntryCreated/Updated` integration events, mapped from domain events (not from read models), carrying the state consumers need, versioned, published through the outbox, and pinned by a schema contract test.
- [ ] **1.14 Conformance:** OpenAPI and AsyncAPI conformance tests green for the implemented subset.
- [ ] **1.15 Authorisation policies:** per-operation policies (separate scopes for command and query operations), with tests.
- [ ] **1.16 MCP adapter: Register & Retrieve:** `RimBank.Party.PartyReferenceDataDirectory.Mcp` tools for Register (command) and Retrieve (query, `readOnly`), with a parity test against the HTTP endpoints.
- [ ] **1.17 MCP adapter: remaining operations:** Update and BQ tools; a test that the tool list matches the implemented BIAN operations.

## Stage 2: Product Directory

`RimBank.ProductManagement.ProductDirectory`. Control Record: *ProductDirectoryEntry*.

- [ ] **2.1 Aggregate:** a product definition (for example, a "Retail Current Account" product: currency, fees, interest terms), with unit tests.
- [ ] **2.2 Register (command side):** command, handler, Marten write adapter and `POST` endpoint.
- [ ] **2.3 Retrieve (query side):** inline view, query, read port and `GET` endpoint.
- [ ] **2.4 Product lifecycle:** launch and withdraw (Control commands), with events.
- [ ] **2.5 Product catalogue:** async cross-stream projection listing launched products, with a `List` query and endpoint, so channels and agents can discover what can be opened.
- [ ] **2.6 Published language:** versioned product events for consumers such as Current Account, carrying the product terms they need (no callback to Product Directory), with a schema contract test.

## Stage 3: Position Keeping (balances & transaction log)

`RimBank.AccountManagement.PositionKeeping`. Control Record: *FinancialPositionLog*. BQ: FinancialTransactionCapture.

- [ ] **3.1 ADR:** position model, balance types (ledger, available, blocked), **which context and stream owns the authoritative balance used for funds checks** (it must be write-side state, never a projection), and the stream-size/snapshot strategy.
- [ ] **3.2 Aggregate:** `FinancialPositionLog` initiate, with currency-consistent invariants.
- [ ] **3.3 Capture transaction:** debit and credit capture decisions; property test that the balance always equals the sum of transactions.
- [ ] **3.4 PostgreSQL adapter (write side):** append-only streams, with snapshots if the ADR requires them.
- [ ] **3.5 Read models:** balance view (inline) and transaction history view (async, paged), with a property test that the projected balance equals the aggregate's balance for any sequence of events.
- [ ] **3.6 API:** Initiate and Capture (commands); Retrieve balance and Retrieve transactions (queries).
- [ ] **3.7 Published language:** versioned `FinancialTransactionCaptured` integration event, with a schema contract test.

## Stage 4: Current Account

`RimBank.LoansAndDeposits.CurrentAccount`. Control Record: *CurrentAccountFacility*.

- [ ] **4.1 Subscriptions:** `RimBank.LoansAndDeposits.CurrentAccount.Subscriptions` subscribes to the party and product integration events through Wolverine's durable inbox, translates them into Current Account's own reference events and stores them in its own streams. Tests: idempotent on redelivery, tolerant of unknown fields and out-of-order delivery. ADR on the first consumer-side integration.
- [ ] **4.2 Local reference read models:** minimal "known party" and "launched product" views projected from those local events (only the fields Current Account needs, no PII copy), used only as inputs to decisions, rebuildable without Party or Product Directory, with their staleness documented in the ADR.
- [ ] **4.3 Initiate facility:** aggregate and decision that require a known party and a launched product.
- [ ] **4.4 Initiate (command) and Retrieve (query):** persistence, inline facility view and API.
- [ ] **4.5 Position link:** initiating an account opens a Position Keeping log (event-driven, through Position Keeping's own `.Subscriptions` adapter).
- [ ] **4.6 Deposit BQ:** a deposit captures a credit in Position Keeping.
- [ ] **4.7 Payment BQ (withdrawal):** funds-availability rule checked against the authoritative write-side balance chosen in ADR 3.1, never a projection; insufficient funds returns a 422 Problem Details response.
- [ ] **4.8 AmountBlock BQ:** place and release holds that affect the available balance (write side), reflected in the views.
- [ ] **4.9 Charge BQ:** fee application driven by the product terms held in the local product read model (no call to Product Directory).
- [ ] **4.10 Interest BQ:** daily accrual through Wolverine scheduled messages and periodic capitalisation.
- [ ] **4.11 Control:** freeze, unfreeze and close the facility, with closure invariants (zero balance, no blocks).
- [ ] **4.12 BQ Retrieve queries:** async views for deposits, payments, blocks, charges and interest, with Retrieve queries and endpoints.
- [ ] **4.13 Conformance + authorisation policies.**
- [ ] **4.14 MCP money-moving tools:** Deposit and Payment tools behind a dedicated scope, disabled by default, requiring an idempotency key, and annotated `destructive`.

## Stage 5: Payments (internal transfers first)

`RimBank.Payments.PaymentOrderInitiation`. Control Record: *PaymentOrderInitiationTransaction*. BQs: OrderInitiation, Compliance, Confirmation.

- [ ] **5.1 Aggregate:** initiate a payment order (ISO 20022 types: debtor, creditor, amount, remittance information).
- [ ] **5.2 Compliance BQ:** sanctions-screening port with an allow-list stub adapter.
- [ ] **5.3 Internal book transfer (Payments side):** Wolverine saga in Payment Order Initiation that never sends Current Account's commands and never reads its views. It publishes `PaymentOrderDebitRequested` and then `PaymentOrderCreditRequested` in its own `.PublishedLanguage`, and reacts to Current Account's outcome events through `RimBank.Payments.PaymentOrderInitiation.Subscriptions`. If the credit is rejected, it publishes `PaymentOrderDebitReversalRequested` (compensation). Saga tests cover the happy path, a rejected debit, a rejected credit with reversal, and redelivered events.
- [ ] **5.4 Internal book transfer (Current Account side):** `RimBank.LoansAndDeposits.CurrentAccount.Subscriptions` turns those requests into Current Account's own `ExecutePayment`, `ExecuteDeposit` and reversal commands, using the payment order ID as the idempotency key. Current Account's rules decide (funds availability, frozen or closed facility), and it publishes `CurrentAccountPaymentExecuted/Rejected` and `CurrentAccountDepositExecuted/Rejected`. Tests: a redelivered request moves money only once, and a rejection carries a stable error code.
- [ ] **5.5 Confirmation BQ:** status lifecycle (initiated → screened → executed / rejected), driven by Current Account's outcome events, with an inline status view and Retrieve query.
- [ ] **5.6 API + Published language + conformance:** including schema contract tests for the request and outcome events on both sides.
- [ ] **5.7 Standing Order:** new context `RimBank.Payments.StandingOrder`, scheduled internal transfers that publish payment order requests (no direct calls to Payment Order Initiation's core).
- [ ] **5.8 Direct Debit Mandate:** new context `RimBank.Payments.DirectDebitMandate`.

## Stage 6: Finance

`RimBank.Finance.FinancialAccounting`.

- [ ] **6.1 Chart of accounts:** minimal general-ledger structure.
- [ ] **6.2 GL posting:** double-entry postings derived from `FinancialTransactionCaptured` events received through Financial Accounting's `.Subscriptions` adapter (command side), with a property test that debits equal credits.
- [ ] **6.3 Trial balance:** async read model and Retrieve query API.
- [ ] **6.4 Reconciliation:** Position Keeping totals vs GL, compared at matching event checkpoints so projection lag cannot cause false breaks. It is its own `AccountReconciliation` context (to be decided in an ADR) with its own copies of both sides' totals, fed by their published events; it never reads their schemas.

## Stage 7: Production hardening (cross-cutting, before the first release)

- [ ] **7.1 Rate limiting:** 429 Problem Details, as declared by BIAN.
- [ ] **7.2 PII protection:** masking in logs and protection of sensitive fields at rest (event streams and read models).
- [ ] **7.3 Audit query API:** who did what and when, served by an async audit read model over the event streams.
- [ ] **7.4 Read-model operations:** async daemon health checks, projection-lag metrics and alerts (OpenTelemetry), and a tested runbook for a zero-downtime projection rebuild.
- [ ] **7.5 Container images:** non-root images, plus SBOM and signed release in GitHub Actions.
- [ ] **7.6 Performance baseline:** load test on the Deposit, Withdrawal and Transfer flows (commands) and the balance and history queries.
- [ ] **7.7 Mutation testing:** Stryker.NET on the money-moving cores.
- [ ] **7.8 Threat model:** document and ADRs.
- [ ] **7.9 MCP hardening:** rate limiting per MCP client, tool-call audit query, and an example agent configuration (for example, Claude Code `.mcp.json`) in `docs/`.
- [ ] **7.10 Release v0.1.0:** the retail core works end to end over HTTP and MCP.

## Stage 8+: Growth backlog (order to be decided when Stage 7 is done)

Each item repeats the slice pattern used for Stage 1: ADR → aggregate → command flow → PostgreSQL write side → command API → query flow and read models → BQs → published language → conformance → MCP.

- Savings Account, Term Deposit
- Party Lifecycle Management (onboarding / KYC), Customer Agreement
- Customer Product and Service Eligibility, Customer Offer, Sales Product Agreement
- Consumer Loan, Loan
- Fraud Evaluation, Regulatory Reporting
- Payment Rail adapters (for example, SEPA/ISO 20022 pacs messages)
- Customer Position: a query-only context fed by integration events that gives a customer overview across Party, Current Account and Payments (the answer to cross-context queries, instead of joins)
- A dedicated read store for a high-volume query (only if the performance baseline in 7.6 shows it is needed, with an ADR)
- Extracting a first context into its own service over RabbitMQ (this proves the modular-monolith promise)

# RimBank — Roadmap

> Part of the RimBank constitution. See also [mission.md](mission.md) and [tech-stack.md](tech-stack.md).
> This roadmap gives the high-level implementation order. Each phase is deliberately small: one pull request, about one focused day of work.

## Definition of done (every phase)

- [ ] Working code that follows [tech-stack.md](tech-stack.md) (naming, dependency rules, business-only names in core)
- [ ] Unit tests for domain and application logic, plus integration tests (Testcontainers) for any adapter touched
- [ ] Architecture tests green
- [ ] Every new error path returns RFC 9457 Problem Details (on both HTTP and MCP)
- [ ] Every new BIAN operation exposed over HTTP is also exposed as an MCP tool, with a parity test
- [ ] GitHub Actions CI green (build, test, format, CodeQL)
- [ ] An ADR in `docs/adr/` for any significant or BIAN-interpretation decision
- [ ] The roadmap checkbox ticked in the same PR

---

## Stage 0: Foundations

- [ ] **0.1 Repository skeleton:** `RimBank.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, MIT `LICENSE`, `README.md`, `docs/adr/0001-record-architecture-decisions.md`.
- [ ] **0.2 CI pipeline:** GitHub Actions workflow for restore, build, test and `dotnet format --verify-no-changes`; Dependabot; CodeQL.
- [ ] **0.3 SharedKernel: Result & Error:** `Result<T>`, `Error` (code, message, kind), with unit tests.
- [ ] **0.4 SharedKernel: typed identifiers:** base pattern for `Guid` v7 strongly typed IDs.
- [ ] **0.5 SharedKernel: Money & Currency:** ISO 4217 currency, minor units, rounding policy, with arithmetic property tests (FsCheck).
- [ ] **0.6 SharedKernel: decider abstraction:** `Decide` / `Evolve` contracts and test helpers (Given events / When command / Then events).
- [ ] **0.7 Architecture tests:** ArchUnitNET project enforcing the dependency table and forbidden folder names. ADR on the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>`.
- [ ] **0.8 Aspire AppHost + ServiceDefaults:** empty `RimBank.Host` with a health endpoint, OpenTelemetry, and a PostgreSQL resource.
- [ ] **0.9 Problem Details baseline:** global exception handler, `Error` → Problem Details mapping convention, 404/500 tests asserting `application/problem+json`.
- [ ] **0.10 Marten wiring:** event store in the host with a per-context schema convention and a Testcontainers smoke test.
- [ ] **0.11 Wolverine wiring:** local durable queues, transactional outbox with Marten, and a smoke test that publishes and handles a message.
- [ ] **0.12 Idempotency:** `Idempotency-Key` middleware or policy, with tests (same key gives the same result, different payload gives 409).
- [ ] **0.13 Authentication baseline:** Keycloak in Aspire, JWT bearer, deny by default, and a test that an unauthenticated request gets a 401 Problem Details response.
- [ ] **0.14 BIAN conformance harness:** a test utility that loads an ext-DDD OpenAPI/AsyncAPI YAML and asserts that the implemented routes and channels exist.
- [ ] **0.15 MCP wiring:** `/mcp` Streamable HTTP endpoint in `RimBank.Host` using the MCP C# SDK, with a `ping`-style smoke tool and an in-process MCP client test. ADR on tool naming, annotations and error mapping.
- [ ] **0.16 MCP security & errors:** MCP authorization through Keycloak, the `Error` → Problem Details mapping as an `isError` tool result, and a test that an unauthenticated call is rejected.

## Stage 1: Party Reference Data Directory (first vertical slice)

`RimBank.Party.PartyReferenceDataDirectory`. Control Record: *PartyReferenceDataDirectoryEntry*. BQs: Demographics, Reference, Associations, BankRelations.

- [ ] **1.1 ADR:** BIAN → DDD mapping for this domain (aggregate boundaries, BQs as entities).
- [ ] **1.2 Aggregate:** `PartyReferenceDataDirectoryEntry` state, `Register` decision, `PartyRegistered` event, invariants, unit tests.
- [ ] **1.3 Register flow (core):** `RegisterParty` command and handler, `PartyDirectory` port, in-memory test adapter.
- [ ] **1.4 PostgreSQL adapter:** Marten event-stream implementation of `PartyDirectory`, with integration tests.
- [ ] **1.5 API adapter: Register:** `POST /PartyReferenceDataDirectory/Register` with validation returning Problem Details.
- [ ] **1.6 API adapter: Retrieve:** inline snapshot projection and `GET /PartyReferenceDataDirectory/{id}/Retrieve` (404 returns Problem Details).
- [ ] **1.7 Update:** `Update` decision, event and endpoint, with optimistic concurrency (409 Problem Details).
- [ ] **1.8 Demographics BQ:** model, decisions, endpoints.
- [ ] **1.9 Reference BQ:** party identifiers (national ID, tax ID, LEI) as value objects.
- [ ] **1.10 Associations BQ:** party-to-party relationships.
- [ ] **1.11 BankRelations BQ:** the party's relationships with the bank.
- [ ] **1.12 Published language:** `PartyReferenceDataDirectoryEntryCreated/Updated` integration events published through the outbox.
- [ ] **1.13 Conformance:** OpenAPI and AsyncAPI conformance tests green for the implemented subset.
- [ ] **1.14 Authorisation policies:** per-operation policies, with tests.
- [ ] **1.15 MCP adapter: Register & Retrieve:** `RimBank.Party.PartyReferenceDataDirectory.Mcp` tools for Register and Retrieve, with a parity test against the HTTP endpoints.
- [ ] **1.16 MCP adapter: remaining operations:** Update and BQ tools; a test that the tool list matches the implemented BIAN operations.

## Stage 2: Product Directory

`RimBank.ProductManagement.ProductDirectory`. Control Record: *ProductDirectoryEntry*.

- [ ] **2.1 Aggregate:** a product definition (for example, a "Retail Current Account" product: currency, fees, interest terms), with unit tests.
- [ ] **2.2 Persistence + Register/Retrieve API.**
- [ ] **2.3 Product lifecycle:** launch and withdraw (Control), with events.
- [ ] **2.4 Published language:** product events for consumers such as Current Account.

## Stage 3: Position Keeping (balances & transaction log)

`RimBank.AccountManagement.PositionKeeping`. Control Record: *FinancialPositionLog*. BQ: FinancialTransactionCapture.

- [ ] **3.1 ADR:** position model and balance types (ledger, available, blocked).
- [ ] **3.2 Aggregate:** `FinancialPositionLog` initiate, with currency-consistent invariants.
- [ ] **3.3 Capture transaction:** debit and credit capture decisions; property test that the balance always equals the sum of transactions.
- [ ] **3.4 PostgreSQL adapter:** append-only streams and balance projection.
- [ ] **3.5 API:** Initiate, Capture and Retrieve (balance and transactions).
- [ ] **3.6 Published language:** `FinancialTransactionCaptured` integration event.

## Stage 4: Current Account

`RimBank.LoansAndDeposits.CurrentAccount`. Control Record: *CurrentAccountFacility*.

- [ ] **4.1 Local read models:** party and product read models fed by the Stage 1 and Stage 2 integration events (no synchronous calls).
- [ ] **4.2 Initiate facility:** aggregate and decision that require a known party and a launched product.
- [ ] **4.3 Persistence + Initiate/Retrieve API.**
- [ ] **4.4 Position link:** initiating an account opens a Position Keeping log (event-driven).
- [ ] **4.5 Deposit BQ:** a deposit captures a credit in Position Keeping.
- [ ] **4.6 Payment BQ (withdrawal):** funds-availability rule; insufficient funds returns a 422 Problem Details response.
- [ ] **4.7 AmountBlock BQ:** place and release holds that affect the available balance.
- [ ] **4.8 Charge BQ:** fee application driven by the product definition.
- [ ] **4.9 Interest BQ:** daily accrual through Wolverine scheduled messages and periodic capitalisation.
- [ ] **4.10 Control:** freeze, unfreeze and close the facility, with closure invariants (zero balance, no blocks).
- [ ] **4.11 Conformance + authorisation policies.**
- [ ] **4.12 MCP money-moving tools:** Deposit and Payment tools behind a dedicated scope, disabled by default, requiring an idempotency key, and annotated `destructive`.

## Stage 5: Payments (internal transfers first)

`RimBank.Payments.PaymentOrderInitiation`. Control Record: *PaymentOrderInitiationTransaction*. BQs: OrderInitiation, Compliance, Confirmation.

- [ ] **5.1 Aggregate:** initiate a payment order (ISO 20022 types: debtor, creditor, amount, remittance information).
- [ ] **5.2 Compliance BQ:** sanctions-screening port with an allow-list stub adapter.
- [ ] **5.3 Internal book transfer:** Wolverine saga that debits account A and credits account B, with compensation if a step fails.
- [ ] **5.4 Confirmation BQ:** status lifecycle (initiated → screened → executed / rejected).
- [ ] **5.5 API + Published language + conformance.**
- [ ] **5.6 Standing Order:** new context `RimBank.Payments.StandingOrder`, scheduled internal transfers.
- [ ] **5.7 Direct Debit Mandate:** new context `RimBank.Payments.DirectDebitMandate`.

## Stage 6: Finance

`RimBank.Finance.FinancialAccounting`.

- [ ] **6.1 Chart of accounts:** minimal general-ledger structure.
- [ ] **6.2 GL posting:** double-entry postings derived from `FinancialTransactionCaptured` events, with a property test that debits equal credits.
- [ ] **6.3 Trial balance read model and API.**
- [ ] **6.4 Reconciliation:** Position Keeping totals vs GL (an `AccountReconciliation` context, to be decided in an ADR).

## Stage 7: Production hardening (cross-cutting, before the first release)

- [ ] **7.1 Rate limiting:** 429 Problem Details, as declared by BIAN.
- [ ] **7.2 PII protection:** masking in logs and protection of sensitive fields at rest.
- [ ] **7.3 Audit query API:** who did what and when, served from the event streams.
- [ ] **7.4 Container images:** non-root images, plus SBOM and signed release in GitHub Actions.
- [ ] **7.5 Performance baseline:** load test on the Deposit, Withdrawal and Transfer flows.
- [ ] **7.6 Mutation testing:** Stryker.NET on the money-moving cores.
- [ ] **7.7 Threat model:** document and ADRs.
- [ ] **7.8 MCP hardening:** rate limiting per MCP client, tool-call audit query, and an example agent configuration (for example, Claude Code `.mcp.json`) in `docs/`.
- [ ] **7.9 Release v0.1.0:** the retail core works end to end over HTTP and MCP.

## Stage 8+: Growth backlog (order to be decided when Stage 7 is done)

Each item repeats the slice pattern used for Stage 1: ADR → aggregate → core flow → PostgreSQL → API → BQs → published language → conformance.

- Savings Account, Term Deposit
- Party Lifecycle Management (onboarding / KYC), Customer Agreement
- Customer Product and Service Eligibility, Customer Offer, Sales Product Agreement
- Consumer Loan, Loan
- Fraud Evaluation, Regulatory Reporting
- Payment Rail adapters (for example, SEPA/ISO 20022 pacs messages)
- Extracting a first context into its own service over RabbitMQ (this proves the modular-monolith promise)

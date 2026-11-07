# 0028. Demographics Exchange as verification of the recorded details

- Status: Accepted
- Date: 2026-11-07
- Roadmap phase: 1.9

## Context

Roadmap 1.9 builds the Demographics Behavior Qualifier of the Party Reference Data Directory: `Demographics/Update`, `Demographics/Exchange` and `Demographics/Retrieve` ([ADR 0024](0024-party-reference-data-directory-mapping.md)). ADR 0024 left the meaning of Exchange to this phase.

What the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives:

- Exchange is `PUT …/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Exchange`, described only as "EcBQ Accept, verify, reject etc. details of a customer directory entry".
- Its body, `ExchangeDemographics`, has exactly the same fields as `UpdateDemographics`. It has no field that says whether the bank accepts, verifies or rejects.

The [mission](../../specs/mission.md#source-of-truth) asks RimBank to stay faithful to the intent of the service domain when BIAN is underspecified, and to record the interpretation in an ADR. A directory entry's details are reference data that a bank checks (KYC, onboarding), so "verify / reject" has a real business meaning. Party Lifecycle Management (Stage 8+) can build on it.

## Options considered

1. **Verification of the recorded details, with an action field.**
   - Exchange takes RimBank's extension field `ExchangeActionType` (`Verify` | `Reject`) and records the outcome on the details already recorded by Update. The BIAN detail fields in its body are ignored.
   - Pros: a clear, testable business meaning that matches "accept, verify, reject". Update and Exchange stay distinct.
   - Cons: it extends BIAN's request body by one field.
2. **Accept details from an outside source.**
   - Exchange takes the same fields as Update (from the customer or a KYC provider) and records them as accepted and verified.
   - Pros: no extension field.
   - Cons: it is close to an alias of Update. A caller cannot reject anything, and it cannot verify details that are already recorded.
3. **Defer Exchange.**
   - Pros: no invented semantics.
   - Cons: the roadmap row for 1.9 names Exchange, and the verification need stays unanswered.

## Decision

Option 1. Exchange is the bank's verification of the party's current demographic details.

- **Request.** `ExchangeActionType` is required and case-sensitive: `Verify` or `Reject`. A missing or blank one is `party.exchange-action-type-required` (400), and any other value is `party.exchange-action-type-invalid` (400). The BIAN fields of `ExchangeDemographics`, `EthnicityReligion` included, are accepted and ignored.
- **Status.** The Demographics record carries a `VerificationStatus` (`Unverified` | `Verified` | `Rejected`) and a `VerificationDate`, the time of the Exchange (`TimeProvider`).
  - A registered entry starts with empty, `Unverified` details.
  - Verify and Reject set the status and the date, and can follow each other: rejected details can be verified later.
  - An Update that **changes** the details sets them back to `Unverified` and clears the date. An Update that changes nothing keeps the verification. It is still recorded, as every accepted Update is (1.8).
- **Rule.** Details that were never recorded, or were all cleared, cannot be verified or rejected: `party.demographics-not-recorded` (BusinessRule, 422).
- **Event.** `DemographicsExchanged(EntryId, DemographicsId, VerificationStatus, VerificationDate)`, appended to the entry's stream with the caller's `If-Match` version ([ADR 0027](0027-optimistic-concurrency-for-commands.md)). `DemographicsUpdated` carries the full resulting details and their resulting verification.
- **Response.** Update, Exchange and Retrieve answer the same body: BIAN's Demographics fields without `EthnicityReligion`, plus `VerificationStatus` and `VerificationDate`, and the entry and Demographics ids. Unknown details are left out.

## Consequences

- One request field (`ExchangeActionType`) and two response fields (`VerificationStatus`, `VerificationDate`) go beyond BIAN's schemas. The 1.14 shape conformance tests list them as RimBank extensions.
- The verification belongs to the Demographics entity of the entry aggregate, so it shares the entry's stream version: an Exchange moves the `ETag` like any other change to the entry (ADR 0024, Concurrency).
- A separate authorisation scope for Exchange (verification is a back-office act) is for 1.15. The MCP tool `party_reference_data_directory_exchange_demographics` takes `exchangeActionType` and `expectedVersion` (1.17).
- **Enforcement:**
  - the decision tests (`ExchangeDemographicsTests`, `UpdateDemographicsTests`) and the entry's FsCheck laws pin the status lifecycle;
  - `ExchangeActionTypesTests` pins the accepted names;
  - the HTTP tests (`ExchangeDemographicsEndpointTests`) pin the 400, 409, 422 and 428 answers.

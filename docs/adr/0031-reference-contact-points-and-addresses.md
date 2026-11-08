# 0031. Reference BQ: postal addresses as structured ISO 20022 parts, contact points read through the ISO 20022 subtypes

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.19

## Context

Roadmap 1.19 records six BIAN Reference fields that [ADR 0029](0029-reference-fields-in-stages.md) refused until now:

- three postal addresses: `ResidentialAddress`, `PartyAddress` and `CorporateAddress`;
- three contact points: `eMailAddress`, `CellPhoneNumber` and `SocialNetworkContacts`.

What the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives:

- **The addresses are `PostalAddress`.** It has 20 parts:
  - structured text parts with ISO 20022 lengths: `Department`, `SubDepartment` (Max70Text); `StreetName`, `StreetBuildingIdentification`, `BuildingName`, `TownName`, `SuiteIdentification`, `BuildingIdentification`, `MailDeliverySubLocation`, `Block`, `Lot` (Max35Text); `Floor`, `PostOfficeBox`, `PostCodeIdentification` (Max16Text);
  - `AddressType` (an `AddressTypeCode`) and `Country` (the `Country` object of [ADR 0030](0030-iso-3166-country-codes.md));
  - nested business objects: `Location`, `ChequeIssue`, `ValidityPeriod`, `MailingInstructions` and `PhysicalDelivery`.
- **The contact points are `ContactPoint`.** In ISO 20022, `ContactPoint` is the abstract base of `PostalAddress`, `ElectronicAddress` and `PhoneAddress`. The YAML gives only the base properties: `Identification`, `BICAddress`, `RelatedParty`, `TemporaryIndicator`, and so on. None of them holds an email address, a phone number or a URL. Their only text candidate, `Identification`, is a `GenericIdentification`, and the YAML does not define it.
- In ISO 20022 those values live on the subtypes: `ElectronicAddress.EmailAddress`, `ElectronicAddress.URLAddress` and `PhoneAddress.MobileNumber`.

The [mission](../../specs/mission.md#source-of-truth) asks RimBank to stay faithful to BIAN's intent where it is underspecified, and to record the interpretation. ISO 20022 payments (CBPR+, SEPA) now require structured addresses with at least a town and a country, and Payments (Stage 5) will send ISO 20022 parties.

## Options considered

1. **Contact points as plain strings** (`"eMailAddress": "jeanne@example.fr"`).
   - Pros: the simplest JSON.
   - Cons: it breaks BIAN's object shape, so the API no longer matches the spec.
2. **Contact points through `ContactPoint.Identification`.**
   - Pros: it uses a property the YAML lists.
   - Cons: `GenericIdentification` is not defined, and an identification is not an address.
3. **Contact points through the ISO 20022 subtype property inside BIAN's object** (`{ "EmailAddress": … }`, `{ "MobileNumber": … }`, `{ "URLAddress": … }`).
   - Pros: it keeps BIAN's object, uses the ISO 20022 meaning and names, and leaves room for the other subtype parts.
   - Cons: those property names are not in BIAN's YAML.
4. **The full `PostalAddress`**, with `AddressType`, `ValidityPeriod` and the minor parts.
   - Pros: the BQ is complete.
   - Cons: several more value objects and rules, with no use for them yet, against principle 10.
5. **Split the phase**: addresses in 1.19, and contact points in a new row.
   - Pros: two smaller phases.
   - Cons: one more roadmap row, for work that fits in one day.

## Decision

Option 3 for the contact points. The addresses use the structured subset below, not option 4. Both are chosen in the 1.19 planning interview.

### Postal addresses

All three fields use one value object, `PostalAddress` (in the Party core, `Reference/`):

| BIAN part | Rule |
|---|---|
| `Department`, `SubDepartment` | Trimmed, at most 70 characters |
| `StreetName`, `StreetBuildingIdentification`, `BuildingName` | Trimmed, at most 35 characters |
| `Floor`, `PostOfficeBox`, `PostCodeIdentification` | Trimmed, at most 16 characters |
| `TownName` | Trimmed, at most 35 characters, **required** |
| `Country.Code` | `CountryCode`, trimmed and upper-cased as in 1.18, **required** |

- **Optional parts.** A blank optional part is not recorded.
- **The structured parts of ISO 20022 payments.** These are the parts that pacs and pain messages use, so a future payment party can be built from them.
- **A town and a country.** They are required, as in the CBPR+ structured-address rule. An address that names no town is not useful to a bank.
- **Refused parts.** `AddressType`, `Location`, `ChequeIssue`, `ValidityPeriod`, `SuiteIdentification`, `BuildingIdentification`, `MailDeliverySubLocation`, `Block`, `Lot`, `MailingInstructions`, `PhysicalDelivery` and the other `Country` parts are refused like `OtherIdentification` (`party.reference-field-not-supported`, with no phase named). The BIAN field name already says what each address is for, so `AddressType` adds nothing.
- **Where `PostalAddress` lives.** It stays in the Party core, as tech-stack §3.4 asks for context value objects. Payments will own its own address type (principle 7). Only `CountryCode` is shared.

### Contact points

| BIAN field | Part read | Value object | Rule |
|---|---|---|---|
| `eMailAddress` | `EmailAddress` | `EmailAddress` | Trimmed, at most 256 characters (Max256Text). Exactly one `@`, a non-empty local part, a domain of at least two non-empty labels, no whitespace, no display name and no `<>()[],;:"\`. Kept as written |
| `CellPhoneNumber` | `MobileNumber` | `MobileNumber` | Spaces, hyphens and dots removed, then E.164: `+`, a first digit from 1 to 9, and 7 to 15 digits in all. Stored normalised (`+33612345678`), so ISO 20022's `+33-612345678` reads the same. Brackets are refused, because removing them from `+33 (0)6…` would keep the trunk `0` and give a wrong number |
| `SocialNetworkContacts` | `URLAddress` | `SocialNetworkAddress` | Trimmed, at most 2048 characters (Max2048Text), an absolute `https` URL with a host. Kept as written |

- **Other parts are refused.** Any other part of these objects, including another subtype's part such as `PhoneNumber` inside `CellPhoneNumber`, is refused with no phase named.
- **One value per field.** BIAN gives one `ContactPoint` per field, so each field holds one current value.
- **A syntax check only.** The email address and the mobile number are checked for their syntax. Whether they reach the party is a verification, which Reference does not have (BIAN gives it no Exchange).

### Updates and errors

- **Partial update, as in 1.18.**
  - A field left out keeps its value.
  - A given object replaces the whole address or contact point.
  - An object whose recorded parts are all blank clears it.
- **Errors.** An invalid value is `party.reference-invalid`, and `errors` names its BIAN path, for example:
  - `ResidentialAddress.TownName`;
  - `CorporateAddress.Country.Code`;
  - `eMailAddress.EmailAddress`;
  - `CellPhoneNumber.MobileNumber`;
  - `SocialNetworkContacts.URLAddress`.
- **Stored form.** The contact points are stored as plain JSON strings, and an address as an object with its country as a plain string.

## Consequences

- **Tests.** `PostalAddressTests`, `EmailAddressTests`, `MobileNumberTests` and `SocialNetworkAddressTests` pin the rules. `PartyReferenceChangesTests` pins the paths and the refused parts.
- **Clients.** A client that sends a contact point in another shape, such as a plain string or `Identification`, gets a 400, never a silent drop.
- **Later additions.** A later ADR can add `AddressType`, validity periods or more contact points without breaking stored events, because the new members are optional.
- **PII.** Addresses, email addresses, phone numbers and profile URLs are personal data. They live only in this context's stream and view, are listed for 7.2, and no code logs them.

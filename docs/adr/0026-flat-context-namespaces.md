# 0026. Context namespaces stop at the context or its adapter

- Status: Accepted
- Date: 2026-11-07
- Roadmap phase: 1.7

## Context

[ADR 0011](0011-namespace-rule.md) set the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]`, and tech-stack §3.4 organises a context's code in folders by aggregate, Behavior Qualifier and flow. Until now every folder was also a namespace segment. One context had twelve namespaces: `RimBank.Party.PartyReferenceDataDirectory.Registration`, `.Retrieval`, `.PartyReferenceDataDirectoryEntry`, `.Demographics`, `.Reference`, `.BankRelations`, `.Api.Registration`, `.Api.Retrieval`, and so on. Test namespaces followed them (`<namespace>.<folder>.Tests`, tech-stack §3.3).

In the review of roadmap 1.7 (PR #119) the project owner objected: `Registration` and `Retrieval` are not subdomains, they are folder names. A namespace should show the business domain, the service domain and, for an adapter, which one (`.Api`, `.Postgresql`, …), and nothing else. Behavior Qualifiers are not subdomains either: in BIAN they are parts of the service domain's Control Record ([ADR 0024](0024-party-reference-data-directory-mapping.md)).

The folder segments also cost something. The aggregate folder `PartyReferenceDataDirectoryEntry/` gave a namespace with the same name as the aggregate's state record, so two files needed `using Entry = …;`. Most core files carried several `using` lines for the context's own folders.

## Options considered

1. **Keep a namespace per folder.** The namespace shows where a type sits, but it mixes organisation with the domain, and folder moves change every caller's `using`.
2. **Namespaces per aggregate and Behavior Qualifier, flows as folders only.** Treats BQs as subdomains, which BIAN does not.
3. **One namespace per context part:** the core is `RimBank.<BusinessDomain>.<ServiceDomain>`, and each adapter is `….<Adapter>`. Folders keep organising the files.

## Decision

Option 3.

- **Production code.** Every core type of a context is in `RimBank.<BusinessDomain>.<ServiceDomain>`, whatever its folder (for example `PartyReferenceDataDirectoryEntry/`, `Registration/`, `Retrieval/`, `Demographics/`). Every type of an adapter is in `….Api`, `….Mcp`, `….Postgresql`, `….PublishedLanguage` or `….Subscriptions`. The folders of tech-stack §3.4 stay as they are.
- **Tests.** A context's test namespace is the namespace under test plus `.Tests`: `RimBank.Party.PartyReferenceDataDirectory.Tests`, `….Api.Tests`, `….Postgresql.Tests`, whatever the test folder. This replaces `<namespace>.<folder>.Tests` for contexts. Composition projects (SharedKernel, Host, Postgresql, …) keep their folder namespaces and their test rule for now.
- **Type names carry the meaning.** Types are already named in business language with their aggregate or Behavior Qualifier as prefix (`PartyReferenceDataDirectoryEntryErrors`, `PartyAssociationView`), so one namespace per part does not cause clashes. A clash is resolved by a better type name, never by a namespace segment.
- **IDE0130** (namespace matches folder) is turned off in `.editorconfig`, with a pointer here.

## Consequences

- A new architecture rule, `context-namespaces-stop-at-the-adapter`, fails any type whose context namespace goes below the core or its adapter. A bad fixture (`RimBank.SampleBanking.FlowFolderNamespace.Registration`) proves it bites. `test-namespaces` applies the same check to test namespaces of a context.
- `business-folder-names` can no longer see a context's folders in its namespaces. It keeps checking namespaces (now only composition ones can have folder segments, and its fixture moves to `RimBank.SharedKernel.Services`), and `BusinessFolderNamesOnDiskTests` checks the folders of `src/` and `tests/` on disk.
- The `using Entry = …;` alias is gone, and the context's own `using` lines with it. Test examples whose field names equalled a type name (`EntryExamples.DemographicsId`) are renamed (`Demographics`), because the types now share the enclosing namespace.
- Nothing stored depends on the namespaces. Events have stable stored names (`MapEventType`), views are stored by type name, and the idempotency fingerprint is the command's JSON. A message type persisted by Wolverine would carry its full name, but no Party message is persisted yet.
- Amends ADR 0011 (namespace rule), ADR 0023 (core test namespaces) and ADR 0025 (API namespaces). tech-stack §3.1, §3.3 and §3.4 and CONTRIBUTING are updated.

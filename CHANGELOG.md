# Changelog

All notable changes to RimBank are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Until 1.0.0, a minor version may contain breaking changes.

GitHub release notes are generated from merged pull requests and grouped by their `type:` label ([.github/release.yml](.github/release.yml)). This file keeps the summary that people read.

## [Unreleased]

### Added

- **Stage 0, Foundations:** solution skeleton, CI and CodeQL, the SharedKernel (`Result`/`Error`, typed ids, `Money`/`Currency`, decider and projection), architecture tests, the .NET Aspire AppHost and ServiceDefaults, RFC 9457 Problem Details, Marten and Wolverine wiring, idempotency keys, the authentication baseline, the BIAN conformance harness and MCP wiring.
- **Stage 1, Party Reference Data Directory:** the event-sourced `PartyReferenceDataDirectoryEntry` with its Demographics, Reference, Associations and BankRelations Behavior Qualifiers, every in-scope BIAN operation over HTTP and as MCP tools with parity, per-operation authorisation policies, and the published integration events.
- The public open-source repository set-up: governance, support, third-party notices, OpenSSF Scorecard and dependency review ([ADR 0041](docs/adr/0041-open-source-repository-governance.md)).

[Unreleased]: https://github.com/KsiProgramming/RimBank/commits/main

# Security policy

RimBank is core banking software, so we take security reports seriously.

## Supported versions

No release has been published yet. Until v0.1.0, only the `main` branch is supported, and fixes land there.

## Reporting a vulnerability

**Do not open a public issue, discussion or pull request for a security problem.**

Report it privately through [GitHub private vulnerability reporting](https://github.com/KsiProgramming/RimBank/security/advisories/new) ("Security" tab → "Report a vulnerability"). Please include:

- what is affected (service domain, endpoint or MCP tool, commit),
- steps to reproduce or a proof of concept,
- the impact you expect (for example, money created or lost, data exposure, authorisation bypass).

We aim to acknowledge reports within 5 working days and to agree a disclosure timeline with you. We will credit you in the advisory unless you ask us not to.

## Scope

In scope: the code in this repository, its build and CI configuration, and its default deployment settings.

Out of scope: vulnerabilities in third-party dependencies that are already reported upstream (please still tell us if RimBank is exposed), and issues that need a compromised host or administrator credentials.

## Disclaimer

RimBank is **not a licensed bank** and provides no regulatory certification. Adopters are responsible for their own security assessment, compliance and operations.

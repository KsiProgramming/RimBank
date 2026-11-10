# Third-party notices

RimBank's own code and documentation are licensed under the [MIT License](LICENSE). This repository also redistributes third-party material under its own licence, listed below.

## BIAN Semantic APIs, release 14.0.0

- **Location:** [`doc-0/release14.0.0/`](doc-0/release14.0.0/)
- **Source:** [github.com/bian-official/public](https://github.com/bian-official/public), folder `release14.0.0`
- **Copyright:** Banking Industry Architecture Network (BIAN), [bian.org](https://bian.org)
- **Licence:** [Apache License 2.0](doc-0/release14.0.0/LICENSE)
- **Changes:** none. The OpenAPI and AsyncAPI files are kept as published, as the read-only source of truth for service domains, operations and events.

RimBank is an independent project. It is not affiliated with, endorsed by or certified by BIAN. "BIAN" is used only to say which specification RimBank implements.

## NuGet packages

RimBank's runtime and test dependencies are NuGet packages, restored at build time and not committed to this repository. Their versions are pinned in [`Directory.Packages.props`](Directory.Packages.props), and each package carries its own licence. Pull requests that add a dependency under a strong copyleft licence (GPL, AGPL) are blocked by the `dependency-review` workflow.

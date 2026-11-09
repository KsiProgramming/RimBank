---
name: update-docs
description: Keep RimBank's architecture and business documentation (docs/README.md, docs/architecture/*, docs/business/*) in step with the code. Works out which documents and Mermaid diagrams a change affects (a new service domain, BIAN operation, Behavior Qualifier, field, enum value, event, error code, table, dependency, policy, MCP tool, deployment or security change), updates or adds them, renders every diagram to prove it parses, and checks every link. Use this before any pull request is opened (open-phase-pr calls it), when a phase is planned (plan-feature lists its doc tasks), and whenever the user asks to add, update, refresh, check or review the documentation, the diagrams, the C4, ERD, sequence, flow, glossary or value streams, even if they don't say "update-docs".
---

# Update the architecture and business documentation

`docs/` describes `main`. A pull request that changes behaviour shown in a document updates that document in the same pull request (`docs/README.md` "Conventions"). Code and ADRs win when they disagree with a document, so fix the document, never the other way round.

## 1. Find what changed

- Base: `git merge-base HEAD origin/main`. Read `git diff --stat <base>...HEAD` and `git log <base>..HEAD --oneline`.
- When planning (called from `plan-feature`), use the phase's `requirements.md` scope instead of a diff.
- Read the changed core, adapter and host files, and any new or changed ADR. Use the ADRs and code as the source of facts. Never use memory of earlier versions.

## 2. Map the change to documents

Use this table. A change usually touches several rows. "Arch" means `docs/architecture/`, "Biz" means `docs/business/`.

| Change in the diff | Update |
|---|---|
| **New service domain** (new folder in `RimBank.Core`, new `.Postgresql` / `.PublishedLanguage`, new module) | Arch 02 (containers, a new component diagram for the context, the code-level class diagram), Arch 03 (logical ERD, event model, schema, role), Arch 06 (cross-context data flow: move it from *(target)* to today), Arch 07 (project graph, context map), Arch 08 (Aspire topology, role connection string), Arch 09 (readiness, timeline). Biz 01 (capability status), Biz 02 (**new glossary section** for its Control Record and BQs), Biz 03 (relationships to Party and others), Biz 04 (value stream stage status, matrix), Biz 05 (use cases it enables). `docs/README.md` if a new document is added |
| **New BIAN operation** (endpoint + command or query + MCP tool) | Arch 04 (sequence if it adds a new pattern, otherwise the operation table), Arch 05 (decision tree), Arch 08 (policy/scope table if new scopes). Biz 02 (operations, errors), Biz 05 (the use cases that use it) |
| **New or changed field, value object, enum value** | Arch 03 logical ERD attributes. Biz 02 member table or enum table (definition, ISO/BIAN format, rules, business use, PII class). Biz 03 if it links parties |
| **New domain event or event type name** | Arch 03 event model (with its Marten type name from `…Postgresql.Configure`) |
| **New or changed integration event / channel** | Arch 03 published language section, Arch 06 data flows, Arch 07 context map. Biz 02 events section, Biz 05 consumer guide |
| **New decision rule or error code** | Arch 05 decision tree, in the code's real order, with the exact error code. Biz 02 error table |
| **New lifecycle or status** | Arch 05 state machine. Biz 02 enum meaning |
| **New read model / projection** (inline or async) | Arch 03 read models table (and physical table), Arch 06 CQRS paths, Arch 05 rebuild if the procedure changes |
| **Project reference, package, dependency rule, architecture test** | Arch 07 graph, matrix, rules table, packages. Arch 01 decisions if an ADR changed |
| **Request pipeline** (headers, filters, idempotency, concurrency, auth order) | Arch 05 §1 pipeline, Arch 04 sequences 1–4 and 9 |
| **Security** (scopes, policies, MCP auth, PII handling, secrets) | Arch 08 controls and STRIDE, Arch 06 data classification |
| **Deployment / observability / CI** | Arch 08 |
| **New ADR** | Arch 01 key decisions table if it is architecturally significant. Any document that cites a superseded ADR |
| **Roadmap phase completed** | Status lines and *(target)* markers in every document that mentions it. Arch 09 readiness and risks. Biz 04 stage status. Root `README.md` status line |
| **Pure refactor, tests only, internal rename with no behaviour change** | Usually nothing. Still grep `docs/` for renamed type, file or route names |

Then grep for stale names. Every type, route, tool, error code or table the diff renamed or removed must not survive in `docs/`:

```bash
grep -rn "<old name>" docs/ README.md
```

## 3. Edit

- Keep each document's structure and voice: tables, short sentences, and BIAN and banking vocabulary. The business documents are for bank analysts and architects, so explain business meaning there, not C# detail.
- Mark anything not yet on `main` as *(target)*, and remove the marker when it ships.
- Business definitions: quote BIAN verbatim from `doc-0/release14.0.0/apis-iso20022_ext-ddd/`. Cite the exact standard (ISO 20022 `MaxNText`, ISO 17442, ISO 3166-1 alpha-2, ISO 4217, ISO 8601, ITU-T E.164, RFC 9457). Say clearly when a meaning is RimBank's interpretation.
- Every new field gets a PII class in Biz 02 and, if personal, a row in the Arch 06 classification.
- Link ADRs and specs with relative paths, as the existing documents do.
- Add a new document only when no existing one fits, and then add it to `docs/README.md` and to the folder's own README.

### Mermaid rules (GitHub renders these)

- Never put `;` in sequence diagram messages or notes: it ends the statement. Use a comma.
- In flowcharts, quote a label that contains `(`, `)`, `[`, `]`, `{`, `}`, `:` or `#`: `A["text (detail)"]`.
- In `erDiagram`, entity names and attribute types are single tokens (letters, digits, `_`). Put explanations in the quoted comment.
- Use `<br/>` for line breaks in labels.

## 4. Verify (mandatory)

Render every Mermaid block in `docs/`. A diagram that does not parse shows as an error on GitHub.

```bash
S=<scratchpad>
mkdir -p "$S/mmd" "$S/out"
for f in docs/architecture/*.md docs/business/*.md; do
  awk -v base="$S/mmd/$(basename $f .md)" '/^```mermaid/{n++; out=base"__"n".mmd"; inb=1; next} /^```/{if(inb){inb=0; close(out)}; next} inb{print > out}' "$f"
done
# one-off install in the scratchpad: npm install @mermaid-js/mermaid-cli@11.4.2
# no bundled Chrome: point puppeteer at Edge or Chrome, e.g.
# {"executablePath":"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe","headless":"new"} > "$S/pptr.json"
for f in "$S"/mmd/*.mmd; do
  "$S/tool/node_modules/.bin/mmdc" -q -p "$S/pptr.json" -i "$f" -o "$S/out/$(basename $f .mmd).svg" || echo "FAIL $f"
done
```

Check that every relative link resolves:

```bash
for f in docs/README.md docs/architecture/*.md docs/business/*.md README.md; do d=$(dirname $f)
  grep -oE '\]\([^)#]+(#[^)]*)?\)' "$f" | sed -E 's/^\]\(//; s/\)$//; s/#.*//' | grep -v -E '^(https?:|mailto:)' | sort -u |
  while read l; do [ -n "$l" ] && [ ! -e "$d/$l" ] && echo "BROKEN in $f: $l"; done; done
```

For a large diagram change, render it to PNG (`-o x.png -w 1800 -b white`) and look at it.

## 5. Report

Tell the caller, briefly:

- the documents updated or added, with one line each on what changed;
- the rows of the table that applied but needed no change, and why;
- diagram render count (`n/n`) and link check result;
- any fact you could not confirm from code or ADRs (leave it out of the document, or mark it as an assumption).

When called from `open-phase-pr`, commit the documentation changes on the phase branch before the push (message: what the docs now cover, ending with the session's attribution line), and add a **Documentation** line to the PR body's Validation section: the documents updated, or "No documentation impact" with the reason.

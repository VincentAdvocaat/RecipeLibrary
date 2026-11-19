# Architect output templates

## Implementation plan

```markdown
# <Feature / epic title> — implementation plan

## Context
What exists today (with file paths), what the requirement asks, links (AB#…, E-number).

## Acceptance criteria
Numbered, verifiable statements. These feed the requirements-test-designer skill.

## Approach
The recommended approach only. Describe the vertical slice:
Domain → Application (commands/queries + handlers) → Infrastructure → Web (endpoint/page).

## Key decisions
Each decision one line; rejected alternatives one line of rationale each.
Volatile/reversible decisions first.

## Data model changes
Entities, relationships, migration name(s). Expand–migrate–contract steps if live data exists.

## Files to modify
Grouped by purpose, with paths. New files marked (new).

## Out of scope
Each item with a reason. This section is mandatory.

## Risks and open questions
Each open question with an owner (user decision vs. investigate during implementation).

## Verification
Each item: a command plus the expected result
(e.g. `dotnet test` green; `dotnet ef migrations has-pending-model-changes` reports none).
```

## ADR (Nygard style — keep under one page)

```markdown
# ADR-NNNN: <Title>

**Status:** Proposed | Accepted (YYYY-MM-DD)

## Context
The forces at play. Concrete pain, with numbers or file paths where possible.

## Decision
What we will do, including the migration strategy if applicable.

## Consequences
**Positive:** …
**Negative:** … (honest costs)
```

Write ADRs to `docs/adr/NNNN-<slug>.md`. Triggers: new external dependency, DB schema change, API contract change, infrastructure change, technology choice.

## Alternatives table

```markdown
| Approach | Pros | Cons | Verdict |
|---|---|---|---|
| Status quo | … | … | Rejected |
| Option B | … | … | Rejected |
| Option C | … | … | **Selected** |
```

## Plan review rubric

When asked to review a plan (yours or someone else's), score six dimensions 1–5 and list the evidence for each score. Re-check after revisions.

```
PLAN TRIAGE:
  Completeness    x/5  (missing error handling? rollback? states?)
  Feasibility     x/5  (unproven dependencies? untested assumptions?)
  Scope           x/5  (premature abstractions? gold-plating?)
  Testability     x/5  (verification strategy per criterion?)
  Risk            x/5  (blast radius clear? reversible?)
  Assumptions     x/5  (unstated assumptions made explicit?)
```

"Looks consistent" is not a pass — each check needs evidence (a file path, a command output, a quoted requirement).

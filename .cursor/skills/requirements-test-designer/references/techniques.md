# Test design techniques

## Element → technique trigger table

| Requirement element | Technique(s) |
|---|---|
| Input fields with ranges/lengths/formats | Equivalence partitioning + boundary value analysis |
| Interdependent conditions (if A and B then …) | Decision table (collapse impossible columns, state why) |
| 3+ independent parameters | Pairwise (explain the reduction) |
| Object lifecycle / status changes | State transition — include invalid transitions and skip-step attempts |
| Roles and ownership | Authorization matrix (role × action) |
| Acceptance criteria in story form | Gherkin scenarios (Scenario Outline + Examples for variants) |

## Boundary value analysis

For every bounded input use the 6-point formula: `min−1, min, min+1, max−1, max, max+1`. State whether the range is inclusive — if the requirement doesn't say, record it as an assumption. Also cover: empty, null/missing, whitespace-only, exactly-at-limit collection sizes (0, 1, max), and Unicode/diacritics for text (Dutch content: `é`, `ï`).

## Equivalence partitioning

Partition inputs into classes where the system must behave identically; one representative per class. Invalid classes each get their own case (never combine two invalid inputs in one case).

## Decision tables

Columns = condition combinations, rows = conditions then actions. Collapse columns that are impossible or equivalent, and note why. Each remaining column becomes one test case — usually a single parameterized unit test.

## State transitions

Draw states and events first. Cases: every valid transition, every invalid event per state (expect rejection, not silence), and re-entry/idempotency (same event twice).

## Authorization matrix

For user-owned data, always generate the full grid:

| Action | Anonymous | Authenticated non-owner | Owner | Admin |
|---|---|---|---|---|
| Read | 401/redirect | 403/404 | 200 | per requirement |
| Create | 401/redirect | n/a | 201 + owner auto-assigned | … |
| Update | 401/redirect | 403/404 | 200 | … |
| Delete | 401/redirect | 403/404 | 204 | … |

Include IDOR probes (non-owner requests owner's resource ID directly) and "client cannot override owner" (posted owner field is ignored).

## Category completeness checklist (per requirement)

- Happy path (at least one; the Smoke=Yes candidates)
- Validation / negative (one invalid input per case)
- Authorization (grid above)
- Boundaries (6-point formula)
- Persistence / data integrity (saved, reloaded, relationships intact, duplicates handled)
- API contract (status codes, ProblemDetails shape, serialization)
- UI states (loading, empty, error, success, disabled during submit)
- Localization (labels from resx in `nl-NL`, no hardcoded strings, long-string overflow)
- Concurrency (two users/sessions mutating the same aggregate) — only when state is shared

## Gherkin form (for acceptance criteria)

Declarative business language, no UI mechanics ("the user is logged in as the recipe owner", not "click #submit"). `Scenario Outline` + `Examples` tables are the executable rendering of an equivalence or decision table. Tags map to the matrix: `@smoke`, `@negative`, `@authorization`. Use Gherkin as documentation format only — this repo has no SpecFlow/Reqnroll runner.

## Risk-driven density

| Risk | Density |
|---|---|
| High (auth, data loss, money, privacy) | ≥3 cases, P1, include abuse cases |
| Medium (core functionality) | Happy + key negatives, P2 |
| Low (cosmetic, admin-only) | 1 representative case, P3 |

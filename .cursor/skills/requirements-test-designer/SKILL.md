---
name: requirements-test-designer
description: Turn functional requirements or acceptance criteria into a test strategy and a traceable test case matrix, allocated across test levels (xUnit unit, Testcontainers integration, bUnit component, Playwright E2E). Use when the user provides requirements, a user story, an epic, or acceptance criteria and asks for test cases, a test matrix, a test plan, test scenarios, or coverage analysis. Not for implementing the tests themselves (that follows after the matrix is approved).
---

# Requirements Test Designer

Turn requirements into a reviewable test model before any test code is written. The primary deliverable is a test analysis plus a traceable test case matrix — writing the actual tests is a follow-up step that uses the matrix as its spec.

## Project context (update when copying this skill to another project)

- Test pyramid in this repo: `RecipeLibrary.Application.Tests` (xUnit unit), `RecipeLibrary.Web.IntegrationTests` (xUnit + Testcontainers MSSQL), `RecipeLibrary.Web.ComponentTests` (bUnit), `RecipeLibrary.Web.E2E.Tests` (Playwright). Shared fixtures in `RecipeLibrary.Testing`.
- Stable UI selectors via `UiTestIds.cs` (`data-testid`); Stryker.NET mutation testing configured (`stryker-config.json`, baseline in `docs/mutation-baseline.md`).
- Cookie Identity auth with fallback policy "authenticated"; recipes are user-owned (ownership/authorization cases are almost always relevant).
- Dutch-first UI via resx (`nl-NL` default) — localization cases apply to every UI-facing requirement.

## Workflow

```
- [ ] 1. Analyze requirements
- [ ] 2. Propose technique stack + test analysis (REVIEW GATE)
- [ ] 3. Generate the test case matrix
- [ ] 4. Allocate each case to a test level
- [ ] 5. Backfill traceability + finalize
```

**1. Analyze.** Normalize each requirement to an ID (`REQ-001`, …) and, where wording is vague, restate it in EARS form ("When <trigger>, the system shall <response>"). Tag the elements: input fields, conditional logic, state/lifecycle, roles/permissions, persistence, API contract, UI states. List assumptions explicitly (e.g. "assumed range is inclusive") and enumerate clarifying questions for genuinely ambiguous points — do not fabricate expected outcomes. A requirement whose expected outcome cannot be determined is a **weak oracle**: flag it as a gap instead of inventing a result.

**2. Propose techniques and pause.** Pick techniques per element using the trigger table in [references/techniques.md](references/techniques.md). Present a short test analysis: requirements list, technique stack with rationale, assumptions/questions, risk classification (high-risk areas get more and higher-priority cases), and an empty traceability matrix. **Stop here and ask the user to confirm before generating cases** — this gate is mandatory unless the user explicitly asked for the full matrix in one go.

**3. Generate the matrix.** Use this format, with globally sequential IDs:

```markdown
| ID | Requirement | Technique | Level | Scenario | Preconditions | Expected result | Priority | Smoke |
|----|-------------|-----------|-------|----------|---------------|-----------------|----------|-------|
| TC-001 | REQ-001 | BVA | Unit | Title at max length (200) accepted | — | Recipe created | P1 | Yes |
```

Per requirement, run the category completeness check: happy path, validation/negative, authorization (unauthenticated / authenticated non-owner / owner / admin), boundaries, persistence/data integrity, UI states (loading/empty/error/success), localization, concurrency where state is shared. Not every category applies to every requirement — but the skip must be a decision, not an omission. Keep negative tests independent: one invalid input per case, so combined invalid inputs cannot mask defects.

**4. Allocate levels.** No existing skill does this, so follow these rules strictly:

| Rule | Level |
|---|---|
| Pure domain/handler logic, validation rules, calculations | **Unit** (xUnit) — assert every rule here, exhaustively |
| Anything crossing DI + EF Core + auth pipeline (persistence, migrations, endpoint contracts, ownership enforcement) | **Integration** (Testcontainers) — one case per behavior, not per input variant |
| Blazor component rendering, parameters, event callbacks, localized labels, state-dependent markup | **Component** (bUnit) |
| One representative user journey per feature, cross-page flows, auth redirects | **E2E** (Playwright) — the tip of the pyramid; never re-assert validation matrices here |

Test economics: input-variant matrices (equivalence classes, boundaries) become a single parameterized unit test (`TheoryData<>`), not N integration tests. If a case could live at two levels, choose the cheaper one and note the trade-off.

**5. Finalize.** Backfill the traceability matrix both ways: every REQ lists its TC-IDs (orphaned requirements = coverage gap), and every TC references a REQ (orphaned tests = scope creep). Report coverage as a percentage plus residual risk. Never claim "full coverage" the matrix does not support — name what is explicitly untested and why. When tests are later implemented, the matrix maps TC-IDs to real test names (e.g. `TC-004 → CreateRecipeCommandHandlerTests.Create_TitleMissing_Throws`); update the matrix when requirements change, don't regenerate it from scratch.

## Guardrails

- No expected result may be invented: derive it from the requirement, the codebase, or flag it as an open question.
- Combinatorial explosion: when parameter combinations exceed ~20 cases, reduce with pairwise and state the reduction rationale.
- Mutation awareness: for logic covered by Stryker, design assertions strong enough to kill boundary and negated-conditional mutants — assert exact values and both sides of each boundary, not just "no exception". Use the surviving-mutant report (`docs/mutation-baseline.md`) as a source of missing cases.
- Test naming convention for the implementation step: `Method_Condition_ExpectedResult`.

## References

| File | Read when |
|---|---|
| [references/techniques.md](references/techniques.md) | Choosing techniques (step 2) or deriving cases (step 3) |
| [references/test-levels.md](references/test-levels.md) | Writing or reviewing actual test code per level (xUnit, Testcontainers, bUnit, Playwright, Stryker) |

---
name: software-architect
description: Analyze functional requirements against the existing codebase and produce an implementation plan, architecture review, or ADR without writing implementation code. Use when the user asks for an implementation plan, architecture design or review, a technology/pattern trade-off, an ADR, epic/feature breakdown, or asks "how should we build X". Not for writing production code (that is the implementation phase) and not for test design (use requirements-test-designer).
---

# Software Architect

Design first, code never. The deliverable of this skill is a plan, review, or decision record — not source code. Do not create or edit production code files while this skill is active; hand the approved plan off to an implementation session.

## Project context (update when copying this skill to another project)

- .NET 10, Clean Architecture: `Domain` → `Application.Abstractions`/`Application.Contracts` → `Application` → `Infrastructure` (implements Abstractions) → `Web` (Blazor Server + minimal APIs in `Program.cs`).
- Custom in-process CQRS bus (`ICommandBus`/`IQueryBus`, handlers under `UseCases/*`) — not MediatR. Follow the existing pattern; do not introduce a mediator library.
- EF Core + SQL Server, migrations only via `dotnet ef migrations add` (see `.cursor/rules/ef-core-migrations.mdc`). ASP.NET Core Identity with cookie auth; fallback policy is authenticated.
- Azure Container Apps + Azure SQL in the cloud; local via Docker SQL container.
- Planning lives in Azure DevOps (`AB#…`); epics are referenced as E-numbers (e.g. E13 units, E14 private recipes).

## Workflow

Track progress with this checklist:

```
- [ ] 1. Orient in the codebase
- [ ] 2. Analyze the requirement
- [ ] 3. Explore alternatives and decide
- [ ] 4. Write the implementation plan (and ADR if triggered)
- [ ] 5. Self-verify before presenting
```

**1. Orient.** Before proposing anything, read the affected layers: the relevant Domain entities, existing handlers in `Application/UseCases`, the endpoints or pages in `Web`, and recent migrations. Map the existing domain language and respect it. State what you found before asking questions. Never propose a structure the codebase already contradicts.

**2. Analyze the requirement.** Restate it as verifiable acceptance criteria. Identify: affected domain boundaries, data model impact, API/contract impact, authorization impact (who owns the data? what happens for non-owners?), and localization impact. If there are more than two significant unknowns, list them as open questions instead of guessing.

**3. Explore alternatives.** Offer 2–3 genuinely different approaches before converging. For each technology or pattern choice, answer: "Why this over the simpler alternative?" Then decide and record rejected alternatives as one-line rationales.

**4. Write the plan.** Use the implementation plan template in [references/templates.md](references/templates.md). Write an ADR (same file) when the decision involves: a new external dependency, a database schema change, an API contract change, an infrastructure change, or a technology choice. An ADR that takes more than 10 minutes to write is too long.

**5. Self-verify.** Before presenting, check:

- Every acceptance criterion maps to at least one plan step.
- Every technology choice answers "why this over the simpler alternative?".
- Every architectural rule in the plan names its enforcement mechanism (analyzer, architecture test, CI check, code review note) — an unenforced contract decays at the first deadline.
- Schema changes follow expand–migrate–contract when data already exists; every migration is reversible.
- The plan has an explicit "Out of scope" section with reasons.

## Decision principles

Apply these, in this order of precedence (current requirements always win first):

1. **Start simple.** Begin with the fewest components that satisfy requirements. A new component must justify itself with a specific bottleneck it resolves — "might need it later" is not justification.
2. **Prefer boring technology.** Choose what the codebase already uses unless a clear, quantified advantage exists. Deviate only on a concrete limitation, not a theoretical one.
3. **Ladder of least code.** For each piece of new code the plan calls for, take the first rung that holds: reuse what is already in the codebase → BCL/framework feature → already-installed dependency → minimum new code. Prefer built-in DI, options, logging, ProblemDetails, health checks, and Identity before adding third-party infrastructure.
4. **Small team = monolith.** Do not propose service extraction, message queues, or caching layers for a small-team app without a measured bottleneck.
5. **Explicit layer boundaries.** If code in `Application` imports `Microsoft.AspNetCore.*` or `Microsoft.EntityFrameworkCore.*` types, the boundary is violated. Domain has no package dependencies.
6. **Duplication over wrong abstraction.** Two similar handlers are cheaper than one premature generic one.
7. **Tracer bullet.** Prefer one thin end-to-end vertical slice (entity → handler → endpoint/page → test) over horizontal foundation work, unless the project cannot run without that foundation.
8. **Respect the existing app model.** Do not rewrite minimal APIs to controllers, or the custom bus to MediatR, without a decision recorded in an ADR.
9. **Measure before optimizing.** For existing-system changes: identify the actual bottleneck with evidence (profile, query plan, log data), never a vague smell. Cite file paths.

## When NOT to act

- Code is stable, untouched, and has no pending feature work → leave it alone; do not propose refactors to match style preference.
- The user asks a small, unambiguous code question → answer directly; a full plan is overhead.
- The request is test design → route to `requirements-test-designer`. UI/UX design → route to `ux-designer`.

## References

| File | Read when |
|---|---|
| [references/templates.md](references/templates.md) | Writing an implementation plan, ADR, or alternatives table |

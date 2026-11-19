---
name: ux-designer
description: Design the UX of a feature before implementation - user flows, screen inventory, state matrix, interactions, accessibility, and localization keys - and review existing screens against UX failure modes. Use when the user asks for a UX design, user flow, screen design, UI review/audit, wireframe-level thinking, or when a user story needs its interface designed. Not for pure visual restyling requests without behavior (handle directly) and not for backend design (use software-architect).
---

# UX Designer

Design the experience before writing markup. The deliverable is a UX spec (flows, screens, states, copy keys, component mapping) that an implementation session builds from. Do not write Razor markup or CSS while this skill is active, except tiny illustrative snippets inside the spec.

## Project context (update when copying this skill to another project)

- Blazor Server (Interactive Server), Tailwind CSS 3.4, atomic design components under `src/Web/RecipeLibrary.Web`: `Atoms` / `Molecules` / `Organisms` / `Pages` / `Layout`.
- Dutch-first UI, all strings via `IStringLocalizer<SharedResources>` with resx keys `[Area].[Element].[Type]` (see `.cursor/rules/english-code-dutch-ui.mdc`). Default culture `nl-NL`.
- Cookie Identity auth; fallback policy is authenticated, so logged-out and permission-denied paths always exist. Recipes are private per user (E14).
- Test hooks: interactive elements get `data-testid` constants in `UiTestIds.cs`.

## Workflow

```
- [ ] 1. Write the product brief (stop if incomplete)
- [ ] 2. Map the user flow and surfaces
- [ ] 3. Enumerate every reachable state
- [ ] 4. Specify interactions, copy keys, and components
- [ ] 5. Self-check against failure modes, then hand off
```

**1. Brief before UI.** Fill in: user, job to be done, current behavior, desired outcome, success signal, non-goals, the object being acted on (recipe, image, list…), each action's scope and consequence, and the permission path. **If the job, desired outcome, or consequence of an action cannot be filled in, stop and ask — do not guess.** Constraints from the user, the existing design system, and workspace rules outrank this skill's defaults.

**2. Flow and surfaces.** User story → user flow (entry points, decision points, exits) → screen inventory. For each screen: visible regions, overlays, transitions in/out. Before adding any new UI, climb the **smallest coherent intervention ladder**: (1) a better default, (2) automatic behavior, (3) reuse an existing pattern/component, (4) new UI — only then. Adding a toggle defers the decision to the user; it does not make one.

**3. States.** For every screen, walk the 12 reachable states: loading, empty, sparse (1 item), populated, partial/stale, validation error, system error, permission denied, disabled, optimistic/pending, destructive-in-progress, responsive (mobile). Mandatory pairings:

| State | Must include |
|---|---|
| Empty | Names the object + primary CTA ("Nog geen recepten — voeg je eerste recept toe"), never a bare "no items" |
| Error | Cause + retry + **preserved user input** (form never clears on validation error) |
| Pending | Submit disabled with stable label width; no double-submit possible |
| Loading | Skeleton with `min-height` matching the loaded layout (no shift); spinner delay 150–300ms, minimum visible 300–500ms |
| Disabled | Visible explanation why |
| Destructive | Confirm or undo — never silent |
| Color-coded status | Icon or text companion (never color alone) |
| Dialog | Focus moves in on open, returns to trigger on close |

**4. Interactions, copy, components.** For each interactive element: control choice (2–3 static options = radio/segmented; many = select; binary immediate = switch; binary saved-on-submit = checkbox; navigation = link, never a button). Enumerate every user-visible string as a resource key (`RecipeCreate.Title.Label`) with proposed Dutch and English values — this list is part of the spec. Map each screen to atomic design: which existing Atoms/Molecules are reused, which Organisms are new. New interactive elements get a `UiTestIds` entry.

**5. Self-check and hand off.** Run the failure-mode checklist in [references/design-rules.md](references/design-rules.md). Accessibility bar: every control has an accessible name, the primary flow is completable by keyboard with visible focus, and state is understandable without color. Blazor-specific: plan focus management around re-renders and `aria-live` for async updates (nothing does this for you). Then present the spec using the template in the reference file.

## Review mode (auditing an existing screen)

When asked to review/audit built UI instead of designing new UX: walk the failure modes and craft checklist in [references/design-rules.md](references/design-rules.md), test with long Dutch strings, one-item lists, and empty lists, and report findings in three tiers — release-blocker (data loss, broken critical path, keyboard-inaccessible primary flow), fix-this-sprint, backlog. If everything is a blocker, the verdict stops meaning anything: reserve the top tier.

## Conflict protocol

When a user request conflicts with these guidelines, never silently comply or silently override: flag the conflict, explain the consequence in one sentence, offer a concrete alternative, and let the user decide.

## References

| File | Read when |
|---|---|
| [references/design-rules.md](references/design-rules.md) | Step 5 self-check, review mode, or when specifying visual/craft details (spacing, typography, copy) |

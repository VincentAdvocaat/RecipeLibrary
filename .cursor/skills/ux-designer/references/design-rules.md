# UX design rules, failure modes, and spec template

## Failure-mode checklist (design gate and review mode)

**Forms**
- Validation error clears the form (data loss) — worst offender, always check first
- No unsaved-changes warning on navigation away from a dirty form
- Submit not disabled while pending → double submit
- Inputs not normalized (trim, case) before validation
- Labels missing or not associated; paste/IME blocked; error not linked via `aria-describedby`; focus not moved to first error

**States and async**
- Missing loading, empty, or error state; layout shifts between states
- Optimistic update without rollback; out-of-order responses showing stale data
- No `aria-live` announcement for async result (Blazor gives you none for free)

**Focus and keyboard**
- Dialog without focus trap; focus not restored to trigger on close
- Focus lost when content appears/disappears after re-render
- Primary flow not completable by keyboard; focus indicator invisible

**Mobile and touch**
- Hover-only affordances; touch targets < 44px; input font < 16px (iOS zoom)

**Localization (Dutch-first — always applicable here)**
- Hardcoded strings instead of resx keys
- Layout breaks with long Dutch/German-length strings
- Dates/numbers not in `nl-NL` format; wrong plural forms
- Language switcher labels translated instead of native (`Nederlands`, `English`)

**Microcopy**
- Vague errors ("Er is iets misgegaan") without cause or action
- Raw exception text leaked to the user (trust + security)
- Button labels not naming the action ("OK" instead of "Recept verwijderen")

**Craft (nothing automated catches these)**
- Enter submits single-field forms; textarea uses Ctrl+Enter
- Scroll position preserved on back/filter/pagination
- Label and control share one hit target
- If it looks clickable, it is clickable — and vice versa
- Adding/removing a list row doesn't make surrounding content jump
- Stress-tested with: long labels, one-item list, empty list

## Visual craft rules (Tailwind-expressible)

- 4px spacing grid; symmetrical padding unless there's a clear visual reason; one radius system everywhere.
- Concentric radius on nesting: outer radius = inner radius + padding.
- One depth strategy per app (borders-only, subtle shadow, layered shadow, or surface tint) — don't mix.
- One accent color; color only for meaning (status, action); 4-level text contrast hierarchy (primary, secondary, muted, faint).
- If a panel works as plain layout, drop the card treatment; cards only when the card is the interaction.
- Typography: body `text-base` (16px) at every breakpoint; `text-sm` only for labels/captions; headings `font-semibold`/`font-medium` (never `font-bold`); max two font weights per view; `tracking-tight` above `text-xl`.
- `tabular-nums` on numbers that change; `gap-*` on flex/grid parents instead of margins between children; controls keep stable dimensions when labels or counts change.
- One primary action per surface; group with hierarchy, spacing, and alignment before reaching for containers.
- Litmus check: can someone scanning only headings, labels, and numbers understand the page?

## UX spec template (handoff output)

```markdown
# <Feature> — UX spec

## Brief
User / Job / Desired outcome / Success signal / Non-goals / Object / Permission path

## User flow
Entry points → steps with decision points → exits. Note logged-out and
permission-denied branches explicitly (fallback policy is authenticated).

## Screen inventory
Per screen: purpose, regions, overlays, entry/exit transitions.

## State matrix
Per screen, a table: state | what the user sees | notes (one row per applicable
state from the 12-state list; mark inapplicable states as N/A with reason).

## Interactions
Per interactive element: control type, behavior, consequence, confirm/undo for
destructive actions, `UiTestIds` name.

## Resource keys
| Key | nl | en-US |
|---|---|---|
| RecipeCreate.Title.Label | Titel | Title |

## Component mapping
Reused Atoms/Molecules (paths) · new Organisms (with justification via the
smallest-intervention ladder) · layout notes (mobile-first breakpoints).

## Accessibility notes
Focus order, keyboard path for the primary flow, aria-live regions, contrast
concerns.

## Open questions
Each with a recommendation.
```

## Direction quality gate

When the work includes visual direction (new surface, restyle): write a 5–7 line direction spec first — goal + audience, tone in 2–3 adjectives, layout strategy, typography, color/surface, motion, and exactly one signature differentiator. Reject directions that aren't specific enough to implement ("clean and modern" is not a direction). Change one variable at a time when iterating.

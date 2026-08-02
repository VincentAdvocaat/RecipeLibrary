# Mutation testing baseline (E17.F2)

Recorded with Stryker.NET **4.16.0** on `feature/stryker-survivor-cleanup` (behavior-boundary tests after pipeline run 116).

```powershell
./scripts/run-stryker.ps1
```

## Scores (current)

| Target | Mutation score | Killed | Survived | Timeout | Previous |
|--------|----------------|--------|----------|---------|----------|
| Application (pilot files) | **71.30%** | 313 | 105 | 5 | 69.51% (run 116: 69.06%) |
| Infrastructure (`RecipeImportUrlSafety`) | **90.77%** | 56 | 3 | 3 | 86.15% (run 116: 84.62%) |

Timeout counts rose slightly (Application 3→5, Infrastructure 0→3). Treat further inflation as a signal to re-check flaky mutants before raising the hard gate.

## What raised the score (this pass)

Behavior-boundary unit tests (no production changes):

- AccessGuard: whitespace-only `ownerUserId` denied (`IsNullOrWhiteSpace`)
- UrlSafety: `IPAddress.IPv6Any` + ULA `fd00::1` blocked
- Parser: list-index qty **20**, juice qty **0** fall-through, complex-line length **exactly 100** not capped
- Matcher: prefix search at normalized length **3**, suggestions at score **0.45** (`>= SuggestionMinScore`)
- Merger: append `SortOrder == max + 1` on non-empty list

## What raised the score (historical)

- Matcher: exact `0.70` / `0.71` fuzzy boundary, `MaxSuggestions`, tie-break order, search fallback, whitespace input (via injectable scorer)
- Merger: prep/case merge keys, `MergeItemIntoList`, empty-list sort order, null-quantity sum
- Parser: blank line, `sap of`, measure adjectives, confidence literals, list-index >20, unit+fraction
- UrlSafety: `.local` / `.internal` hosts, IPv4-mapped / IPv6 ULA / link-local, CGNAT and `172.31` boundaries
- Scorer: empty/exact short-circuits + `StringSimilarity` edges
- AccessGuard: fail-closed authenticated deny suite (unit + SQL integration IDOR for clear/remove/delete-group); GetNextShoppingListName scoped to owner

## Remaining survivors (accepted for now)

| Area | Why |
|------|-----|
| AccessGuard / UrlSafety **string** mutants | Exception/host literals; behavior already typed |
| Scorer boost `Add(0.72/0.78)` statements | Dominated by exact token `StringSimilarity == 1` — dead for Max |
| Levenshtein / Jaro arithmetic noise | Low product risk; revisit when changing algorithms |
| Parser equality/edge on index/`\|\|` | Partial coverage; incremental |
| UrlSafety `ThrowIfNull` + empty-host / Safe Mode on resolve | Tool / null-path noise (empty `Uri.Host` is not constructible on modern .NET) |

## CI / gate decision

| Mode | Decision |
|------|----------|
| Every PR | **No** |
| Opt-in pipeline (`runStryker`) | **Yes** — separate `Stryker` stage, report only (`thresholds.break = 0`); does not gate Deploy |
| Hard gate | **Deferred** — UrlSafety now >90%; Application pilot still climbing |
| Scheduled nightly | **Deferred** — opt-in is enough for E17 closeout |

## Refresh

1. `./scripts/run-stryker.ps1`
2. Open latest `tests/RecipeLibrary.Application.Tests/StrykerOutput/*/reports/mutation-report.html`
3. Update scores above; do not silently ignore new AccessGuard / UrlSafety behavior survivors

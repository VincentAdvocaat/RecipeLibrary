# Mutation testing baseline (E17.F2)

Recorded with Stryker.NET **4.16.0** on the E17 closeout branch (post E16.F3: `RecipeImportUrlSafety` lives in Infrastructure).

```powershell
./scripts/run-stryker.ps1
```

## Scores (current)

| Target | Mutation score | Killed | Survived | Timeout | Previous |
|--------|----------------|--------|----------|---------|----------|
| Application (pilot files) | **69.51%** | 307 | 113 | 3 | 70.71% |
| Infrastructure (`RecipeImportUrlSafety`) | **86.15%** | 52 | 5 | 0 | 81.54% (as Abstractions) |

Application score dipped slightly after fail-closed AccessGuard + expanded deny coverage (more string/auth mutants). No new AccessGuard *behavior* survivors were introduced beyond accepted string literals. UrlSafety improved after the Infrastructure retarget.

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
| UrlSafety `ThrowIfNull` + Safe Mode on resolve | Tool / null-path noise |

## CI / gate decision

| Mode | Decision |
|------|----------|
| Every PR | **No** |
| Opt-in pipeline (`runStryker`) | **Yes** — separate `Stryker` stage, report only (`thresholds.break = 0`); does not gate Deploy |
| Hard gate | **Deferred** — UrlSafety already >80%; Application pilot still climbing |
| Scheduled nightly | **Deferred** — opt-in is enough for E17 closeout |

## Refresh

1. `./scripts/run-stryker.ps1`
2. Open latest `tests/RecipeLibrary.Application.Tests/StrykerOutput/*/reports/mutation-report.html`
3. Update scores above; do not silently ignore new AccessGuard / UrlSafety behavior survivors

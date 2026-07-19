# Mutation testing baseline (E17.F2)

Recorded from branch `feature/stryker-mutation` with Stryker.NET **4.16.0**.

```powershell
./scripts/run-stryker.ps1
```

## Scores (current)

| Target | Mutation score | Killed | Survived | Timeout | Previous |
|--------|----------------|--------|----------|---------|----------|
| Application (pilot files) | **70.71%** | 301 | 106 | 8 | 63.84% |
| Abstractions (`RecipeImportUrlSafety`) | **81.54%** | 53 | 8 | 0 | 58.46% |

## What raised the score

- Matcher: exact `0.70` / `0.71` fuzzy boundary, `MaxSuggestions`, tie-break order, search fallback, whitespace input (via injectable scorer)
- Merger: prep/case merge keys, `MergeItemIntoList`, empty-list sort order, null-quantity sum
- Parser: blank line, `sap of`, measure adjectives, confidence literals, list-index >20, unit+fraction
- UrlSafety: `.local` / `.internal` hosts, IPv4-mapped / IPv6 ULA / link-local, CGNAT and `172.31` boundaries
- Scorer: empty/exact short-circuits + `StringSimilarity` edges

## Remaining survivors (accepted for now)

| Area | Why |
|------|-----|
| AccessGuard / UrlSafety **string** mutants | Exception/host literals; behavior already typed |
| Scorer boost `Add(0.72/0.78)` statements | Dominated by exact token `StringSimilarity == 1` — dead for Max |
| Levenshtein / Jaro arithmetic noise | Low product risk; revisit when changing algorithms |
| Parser equality/edge on index/`||` | Partial coverage; incremental |
| UrlSafety `ThrowIfNull` + Safe Mode on resolve | Tool / null-path noise |

## CI / gate decision

| Mode | Decision |
|------|----------|
| Every PR | **No** |
| Opt-in pipeline (`runStryker`) | **Yes** — separate `Stryker` stage, report only (`thresholds.break = 0`); does not gate Deploy |
| Hard gate | **Deferred** — UrlSafety already >80%; Application pilot still climbing |

## Refresh

1. `./scripts/run-stryker.ps1`
2. Open latest `tests/RecipeLibrary.Application.Tests/StrykerOutput/*/reports/mutation-report.html`
3. Update scores above; do not silently ignore new AccessGuard / UrlSafety behavior survivors

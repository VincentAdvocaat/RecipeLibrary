# Mutation testing baseline (E17.F2)

Recorded from branch `feature/stryker-mutation` with Stryker.NET **4.16.0** (after E17.F1 honesty on `main`, PR #59).

```powershell
./scripts/run-stryker.ps1
```

## Scores (pilot, post-triage)

| Target | Mutation score | Killed | Survived | Timeout |
|--------|----------------|--------|----------|---------|
| Application (pilot files) | **63.84%** | 271 | 125 | 8 |
| Abstractions (`RecipeImportUrlSafety`) | **58.46%** | 38 | 10 | 0 |

### Application pilot — per type

| Type | Killed | Survived | Notes |
|------|--------|----------|-------|
| `ShoppingListAccessGuard` | 10 | 4 (mostly exception **message** strings) | Early-return / missing-item paths tightened |
| `ShoppingListIngredientMerger` | ~27 | ~21 | Accept for now |
| `IngredientMatcher` | ~23 | ~7 | Accept for now |
| `IngredientSimilarityScorer` | ~72 | ~45 | Accept for now |
| `IngredientLineParser` | ~141 | ~48 | Accept for now |

## Survivor triage

### Security-critical

| Area | Decision |
|------|----------|
| AccessGuard early-return when `ownerUserId` is null (list/group/item) | **Covered** — anonymous + `AccessibleByDefault=false` for clear/summary/toggle; anonymous missing-item must not throw |
| AccessGuard authenticated missing item | **Covered** — throws `InvalidOperationException` |
| UrlSafety private ranges / metadata hosts / CGNAT boundaries | **Improved** — extra hosts + boundary addresses that must stay public |
| Remaining AccessGuard / UrlSafety **string** mutants (exception text, blocked-host literals when DNS/IP still blocks) | **Accepted** — type/behavior asserted; literals alone are not the security boundary |

### Accepted noise

- Large survivor counts in similarity scorer / line parser / merger — revisit when changing those algorithms
- UrlSafety IPv6 / `ResolvePublicHttpEndpointAsync` Safe Mode (Stryker compile-error cascade) — tool limitation
- `ArgumentNullException.ThrowIfNull` statement mutants — null is not a production path

## CI / gate decision

See also `docs/testing.md`:

| Mode | Decision |
|------|----------|
| Every PR | **No** |
| Opt-in pipeline (`runStryker`) | **Yes** — report artifact only (`thresholds.break = 0`) |
| Hard gate | **Deferred** — candidate later: AccessGuard-focused score ≥ 80% |

## Refresh

1. `./scripts/run-stryker.ps1`
2. Open latest `tests/RecipeLibrary.Application.Tests/StrykerOutput/*/reports/mutation-report.html`
3. Update scores above; do not silently ignore new AccessGuard / UrlSafety behavior survivors

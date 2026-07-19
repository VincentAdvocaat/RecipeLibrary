# Testing

RecipeLibrary uses a layered test stack:

| Layer | Project | What it covers |
|-------|---------|----------------|
| Unit | `tests/RecipeLibrary.Application.Tests` | Handlers, domain services (fakes, no DB) |
| Integration | `tests/RecipeLibrary.Web.IntegrationTests` | API endpoints, DB persistence via Testcontainers SQL |
| Component | `tests/RecipeLibrary.Web.ComponentTests` | bUnit tests for complex Blazor molecules |
| E2E | `tests/RecipeLibrary.Web.E2E.Tests` | Playwright browser flows (clicks, input, navigation) |

Shared fixtures and seed data live in `tests/RecipeLibrary.Testing`.

E2E tests start the web app as a `dotnet run` subprocess on a random localhost port so Playwright can connect over TCP (Blazor Server requires a real HTTP listener).

## Prerequisites

- .NET 10 SDK
- **Docker Desktop** running (required for integration and E2E tests — Testcontainers starts a SQL Server container)
- For E2E: Playwright browsers (installed automatically on first E2E test run, or manually below)

## Run all tests

From the repo root (or worktree):

```powershell
dotnet test RecipeLibrary.slnx
```

## Run a single layer

```powershell
dotnet test tests/RecipeLibrary.Application.Tests
dotnet test tests/RecipeLibrary.Web.IntegrationTests
dotnet test tests/RecipeLibrary.Web.ComponentTests
dotnet test tests/RecipeLibrary.Web.E2E.Tests
```

## E2E debugging (headed browser)

```powershell
$env:PLAYWRIGHT_HEADED = "1"
dotnet test tests/RecipeLibrary.Web.E2E.Tests
```

## Install Playwright browsers manually

After building the E2E project:

```powershell
powershell tests/RecipeLibrary.Web.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

## UI test selectors

Stable `data-testid` values are defined in `src/Web/RecipeLibrary.Web/Testing/UiTestIds.cs`. E2E and component tests use these constants — not localized UI text.

## CI

Azure Pipelines runs unit tests with Cobertura coverage (visible on the **Code Coverage** tab), plus component and integration tests. Integration tests require Docker (`ubuntu-latest`).

## Mutation testing (Stryker.NET pilot)

Stryker is used as a **quality lamp** on a small set of critical Application modules (E17.F2), not as a full-solution PR gate.

### Why not full-solution / every PR?

- Mutating the whole solution (Infrastructure, Blazor, OCR extractors) is slow and noisy.
- Coverlet line coverage already runs on every main build; Stryker answers a different question (“would a bug survive our asserts?”).
- Until the pilot baseline is stable, a hard PR break would block merges without enough signal.

### Pilot scope (mutated)

| Project | Types |
|---------|--------|
| `RecipeLibrary.Application` | `ShoppingListAccessGuard`, `ShoppingListIngredientMerger`, `IngredientMatcher`, `IngredientSimilarityScorer`, `IngredientLineParser` |
| `RecipeLibrary.Application.Abstractions` | `RecipeImportUrlSafety` |

Out of scope initially: Infrastructure, Blazor, E2E, and large import extractors (e.g. `RecipeTextDocumentExtractor`).

### Run locally

```powershell
./scripts/run-stryker.ps1
# or one target:
./scripts/run-stryker.ps1 -Target Application
./scripts/run-stryker.ps1 -Target Abstractions
```

Requires the local tool manifest (`.config/dotnet-tools.json`). Reports land in `tests/RecipeLibrary.Application.Tests/StrykerOutput/` (gitignored).

Configs:

- `tests/RecipeLibrary.Application.Tests/stryker-config.json`
- `tests/RecipeLibrary.Application.Tests/stryker-config.abstractions.json`

`thresholds.break` is **0** (report-only). Do not raise a PR gate without updating this doc and agreeing a score.

### CI policy (decision)

| Mode | Status |
|------|--------|
| Every PR | **No** — not required |
| Opt-in on Azure Pipelines | **Yes** — parameter `runStryker` on `azure-pipelines.yml` |
| Scheduled / hard gate | **Deferred** — revisit after baseline triage |

See `docs/mutation-baseline.md` for the recorded pilot scores and survivor triage notes.

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

Azure Pipelines runs `dotnet build` and unit, component, and integration tests on push to `main`. Integration tests require a Docker-capable agent (`ubuntu-latest`). E2E tests are run locally only until the Playwright flows are stable.

# Test level implementation patterns (.NET stack)

Read this when implementing tests from an approved matrix, or when reviewing test code.

## xUnit (unit — `RecipeLibrary.Application.Tests`)

- Naming: `Method_Condition_ExpectedResult` (`Create_TitleMissing_Throws`).
- Lifecycle: constructor = per-test setup; `IDisposable`/`IAsyncLifetime` = teardown; `IClassFixture<T>` for expensive shared setup; `ICollectionFixture<T>` + `[Collection]` to share/serialize across classes.
- Variant matrices: `[Theory]` with `TheoryData<T1,T2>` (typed; avoids `object[]` casts). One theory per equivalence/boundary table, one `[Fact]` per distinct behavior.
- Assert exact values (`Assert.Equal`), never `Assert.True(a == b)`. No static/shared mutable state between tests.
- Anti-patterns: testing implementation details (mock call counts without behavior assertions), multiple unrelated assertions hiding independent failures, arrange blocks longer than the behavior justifies (extract builders/fixtures into `RecipeLibrary.Testing`).

## Integration (Testcontainers — `RecipeLibrary.Web.IntegrationTests`)

- Container fixture via `IAsyncLifetime` + `ICollectionFixture` so one MSSQL container serves the collection; reset data between tests rather than restarting containers.
- Belongs here: EF Core mappings + migrations actually applied, endpoint status codes and ProblemDetails, auth pipeline (401/403/redirect), ownership enforcement against the real DB, transactional behavior.
- Does not belong here: input-variant matrices already asserted at unit level — one representative wiring case is enough.
- Never use the EF in-memory provider as a stand-in; it validates neither SQL nor relational constraints.

## bUnit (component — `RecipeLibrary.Web.ComponentTests`)

- Belongs here: render output per parameter set, event callbacks, state-dependent markup (loading/empty/error/disabled), localized labels.
- Register the same services the component resolves: stub `IStringLocalizer<SharedResources>` (assert on resource *keys* or stubbed values, never hardcoded Dutch), fake auth state via `TestAuthorizationContext` for `AuthorizeView` behavior.
- Find elements via `data-testid` constants from `UiTestIds.cs`, not CSS classes.
- Async rendering: use `cut.WaitForState`/`WaitForAssertion` instead of `Task.Delay`.

## Playwright (E2E — `RecipeLibrary.Web.E2E.Tests`)

- Selector ladder: `GetByRole` → `GetByLabel` → `GetByPlaceholder` → `GetByText` → `GetByTestId` last; never raw CSS/XPath.
- Web-first assertions: `await Expect(locator).ToBeVisibleAsync()` (auto-retries). Never `WaitForTimeoutAsync`, never asserting on a pre-fetched boolean.
- Reuse authenticated state (`storageState`) instead of logging in per test. Page objects only when a page appears in >3 tests.
- Scope discipline: one representative journey per feature; E2E failures should indicate broken flows, not re-litigate validation rules.

## Stryker.NET (mutation)

- Config: `stryker-config.json` (+ infrastructure variant); run via `scripts/run-stryker.ps1`; baseline and known survivors in `docs/mutation-baseline.md`. Threshold `break: 0` — informational, not a PR gate.
- Use surviving mutants as missing-test-case detectors: a surviving `>` → `>=` mutant means the boundary itself is untested (add the `min`/`max` exact-value case); a surviving negated conditional means only one branch is asserted.
- Design assertions to kill mutants: exact expected values, both branches of every condition, both sides of every boundary.
- Do not chase 100%: equivalent mutants and logging-only mutations are documented-ignore candidates, not test targets.

<#
.SYNOPSIS
  Runs the Stryker.NET mutation-testing pilot for RecipeLibrary.

.DESCRIPTION
  Restores the local `dotnet-stryker` tool and mutates only the E17.F2 pilot
  modules (not the full solution).

  Targets:
    - Application: ShoppingListAccessGuard, ShoppingListIngredientMerger,
      IngredientMatcher, IngredientSimilarityScorer, IngredientLineParser
    - Abstractions: RecipeImportUrlSafety

  Reports are written under tests/RecipeLibrary.Application.Tests/StrykerOutput/
  (gitignored). This is intentionally NOT a PR gate — thresholds.break is 0.

.PARAMETER Target
  Which pilot config to run: Application, Abstractions, or All (default).

.PARAMETER BreakAt
  Optional mutation-score break threshold (overrides config). Use only when
  deliberately gating a run.

.EXAMPLE
  ./scripts/run-stryker.ps1

.EXAMPLE
  ./scripts/run-stryker.ps1 -Target Application
#>

[CmdletBinding()]
param(
  [Parameter()]
  [ValidateSet("All", "Application", "Abstractions")]
  [string] $Target = "All",

  [Parameter()]
  [Nullable[int]] $BreakAt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
  $common = (git rev-parse --git-common-dir).Trim()
  if (-not $common) { throw "Unable to determine git common directory." }
  $commonPath = (Resolve-Path -LiteralPath $common).Path
  return (Split-Path -Parent $commonPath)
}

# Prefer worktree root when already inside a worktree.
$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) { $repoRoot = Get-RepoRoot }

$testProjectDir = Join-Path $repoRoot "tests\RecipeLibrary.Application.Tests"
if (-not (Test-Path -LiteralPath $testProjectDir)) {
  throw "Test project directory not found: $testProjectDir"
}

Push-Location $repoRoot
try {
  Write-Host "Restoring local .NET tools..."
  dotnet tool restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

  $configs = @()
  if ($Target -eq "All" -or $Target -eq "Application") {
    $configs += @{
      Name = "Application"
      File = "stryker-config.json"
    }
  }
  if ($Target -eq "All" -or $Target -eq "Abstractions") {
    $configs += @{
      Name = "Abstractions"
      File = "stryker-config.abstractions.json"
    }
  }

  Push-Location $testProjectDir
  try {
    foreach ($cfg in $configs) {
      $configPath = Join-Path $testProjectDir $cfg.File
      if (-not (Test-Path -LiteralPath $configPath)) {
        throw "Stryker config not found: $configPath"
      }

      Write-Host ""
      Write-Host "=== Stryker pilot: $($cfg.Name) ==="
      $args = @("stryker", "--config-file", $cfg.File)
      if ($null -ne $BreakAt) {
        $args += @("--break-at", "$BreakAt")
      }

      & dotnet @args
      if ($LASTEXITCODE -ne 0) {
        throw "Stryker failed for $($cfg.Name) (exit $LASTEXITCODE)."
      }
    }
  }
  finally {
    Pop-Location
  }

  Write-Host ""
  Write-Host "Done. Open the latest HTML report under:"
  Write-Host "  $testProjectDir\StrykerOutput"
}
finally {
  Pop-Location
}

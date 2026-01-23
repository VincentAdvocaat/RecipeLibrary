<#
.SYNOPSIS
  Stops a development workstream by removing the worktree for a branch.

.DESCRIPTION
  This is the recommended counterpart to `scripts/start-development.ps1`.
  It:
    - Resolves worktree path `.worktrees/<branch>`
    - Refuses to remove if you're currently inside that worktree (safety)
    - Removes the worktree via `git worktree remove`
    - Runs `git worktree prune`
    - Optionally deletes the local branch

.PARAMETER Branch
  Branch name whose worktree should be removed.

.PARAMETER Force
  Pass `--force` to `git worktree remove` (use with care).

.PARAMETER DeleteBranch
  Also delete the local branch after removing the worktree.

.EXAMPLE
  ./scripts/stop-development.ps1 -Branch feature/azure-iac-sql-infra

.EXAMPLE
  ./scripts/stop-development.ps1 -Branch feature/azure-iac-sql-infra -Force -DeleteBranch
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string] $Branch,

  [Parameter()]
  [switch] $Force,

  [Parameter()]
  [switch] $DeleteBranch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-GitRepo {
  git rev-parse --is-inside-work-tree *> $null
  if ($LASTEXITCODE -ne 0) { throw "Not inside a git repository." }
}

function Get-RepoRoot {
  $root = (git rev-parse --show-toplevel).Trim()
  if (-not $root) { throw "Unable to determine git repository root." }
  return $root
}

function Get-WorktreePath([string] $repoRoot, [string] $name) {
  $segments = $name -split '/'
  $path = Join-Path $repoRoot ".worktrees"
  foreach ($seg in $segments) { $path = Join-Path $path $seg }
  return $path
}

function Is-PathUnder([string] $child, [string] $parent) {
  $c = (Resolve-Path -LiteralPath $child).Path.TrimEnd('\')
  $p = (Resolve-Path -LiteralPath $parent).Path.TrimEnd('\')
  return ($c.StartsWith($p, [System.StringComparison]::OrdinalIgnoreCase))
}

Assert-GitRepo
$repoRoot = Get-RepoRoot

$Branch = $Branch.Trim()
$worktreePath = Get-WorktreePath -repoRoot $repoRoot -name $Branch

if (-not (Test-Path -LiteralPath $worktreePath)) {
  Write-Host "Worktree path does not exist: $worktreePath"
  return
}

if (Is-PathUnder -child (Get-Location).Path -parent $worktreePath) {
  throw "Refusing to remove worktree while your current directory is inside it. cd to the repo root first."
}

$forceArg = @()
if ($Force) { $forceArg = @("--force") }

Write-Host "Removing worktree: $worktreePath"
git worktree remove $worktreePath @forceArg
if ($LASTEXITCODE -ne 0) { throw "git worktree remove failed." }

Write-Host "Pruning stale worktree metadata..."
git worktree prune

if ($DeleteBranch) {
  Write-Host "Deleting local branch: $Branch"
  git branch -D $Branch
}

Write-Host "Done."


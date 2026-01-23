<#
.SYNOPSIS
  Starts a development workstream by creating/using a worktree for a branch.

.DESCRIPTION
  This is the recommended entrypoint for agents/humans.
  It:
    - Validates branch naming policy (feature/<topic> or bugfix/<topic>)
    - Runs `git fetch` (optional, default on)
    - Creates the worktree at `.worktrees/<branch>` by calling `scripts/new-worktree.ps1`
    - Prints the worktree path (and optionally changes directory to it)

.PARAMETER Branch
  Branch name to develop on (e.g. feature/azure-iac-sql-infra).

.PARAMETER Base
  Base branch to branch off when the branch doesn't exist (default: main).

.PARAMETER Remote
  Remote name to fetch from / base resolution (default: origin).

.PARAMETER NoFetch
  Skip `git fetch <remote>`.

.PARAMETER NoCd
  Do not change directory into the worktree.

.EXAMPLE
  ./scripts/start-development.ps1 -Branch feature/azure-iac-sql-infra
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string] $Branch,

  [Parameter()]
  [string] $Base = "main",

  [Parameter()]
  [string] $Remote = "origin",

  [Parameter()]
  [switch] $NoFetch,

  [Parameter()]
  [switch] $NoCd
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

function Assert-BranchNamingPolicy([string] $name) {
  $pattern = '^(feature|bugfix)\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?(\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?)*$'
  if ($name -notmatch $pattern) {
    throw "Branch '$name' violates policy. Use feature/<topic> or bugfix/<topic> (lowercase, digits, hyphen; '/' allowed for subfolders)."
  }
}

function Get-WorktreePath([string] $repoRoot, [string] $name) {
  $segments = $name -split '/'
  $path = Join-Path $repoRoot ".worktrees"
  foreach ($seg in $segments) { $path = Join-Path $path $seg }
  return $path
}

Assert-GitRepo
$repoRoot = Get-RepoRoot

$Branch = $Branch.Trim()
Assert-BranchNamingPolicy -name $Branch

if (-not $NoFetch) {
  Write-Host "Fetching '$Remote'..."
  git fetch $Remote
}

$worktreePath = Get-WorktreePath -repoRoot $repoRoot -name $Branch

if (Test-Path -LiteralPath $worktreePath) {
  Write-Host "Worktree already exists: $worktreePath"
} else {
  $scriptPath = Join-Path $repoRoot "scripts/new-worktree.ps1"
  if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Missing script: $scriptPath"
  }

  Write-Host "Creating worktree for '$Branch'..."
  & $scriptPath -Branch $Branch -Base $Base -Remote $Remote
}

Write-Host "Worktree path: $worktreePath"

if (-not $NoCd) {
  Set-Location -LiteralPath $worktreePath
  Write-Host "Current directory: $worktreePath"
}


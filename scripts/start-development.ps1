<#
.SYNOPSIS
  Starts a development workstream by creating/using a worktree for a branch.

.DESCRIPTION
  This is the recommended entrypoint for agents/humans.
  It:
    - Validates branch naming policy (feature/<topic> or bugfix/<topic>)
    - Runs `git fetch` (optional, default on)
    - Creates the worktree at `.worktrees/<branch>`
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
  # Important: in git worktrees, --show-toplevel returns the *worktree* root.
  # We want the shared repo root (parent of the common .git directory) so that
  # `.worktrees/<branch>` resolves consistently no matter where the script runs.
  $common = (git rev-parse --git-common-dir).Trim()
  if (-not $common) { throw "Unable to determine git common directory." }

  $commonPath = (Resolve-Path -LiteralPath $common).Path
  if (-not $commonPath) { throw "Unable to resolve git common directory path." }

  return (Split-Path -Parent $commonPath)
}

function Assert-BranchNamingPolicy([string] $name) {
  $pattern = '^(feature|bugfix)\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?(\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?)*$'
  if ($name -notmatch $pattern) {
    throw "Branch '$name' violates policy. Use feature/<topic> or bugfix/<topic> (lowercase, digits, hyphen; '/' allowed for subfolders)."
  }
}

function Test-LocalBranchExists([string] $name) {
  git show-ref --verify --quiet ("refs/heads/{0}" -f $name)
  return ($LASTEXITCODE -eq 0)
}

function Test-RemoteBranchExists([string] $remoteName, [string] $name) {
  git show-ref --verify --quiet ("refs/remotes/{0}/{1}" -f $remoteName, $name)
  return ($LASTEXITCODE -eq 0)
}

function Resolve-StartPoint([string] $remoteName, [string] $baseName) {
  if (Test-RemoteBranchExists $remoteName $baseName) {
    return "{0}/{1}" -f $remoteName, $baseName
  }
  # Fallback for repos without a configured remote.
  return $baseName
}

function Get-WorktreePath([string] $repoRoot, [string] $name) {
  $segments = $name -split '/'
  $path = Join-Path $repoRoot ".worktrees"
  foreach ($seg in $segments) { $path = Join-Path $path $seg }
  return $path
}

function Ensure-ParentDirectory([string] $path) {
  $parent = Split-Path -Parent $path
  if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
  }
}

Assert-GitRepo
$repoRoot = Get-RepoRoot
$startPoint = Resolve-StartPoint -remoteName $Remote -baseName $Base

$Branch = $Branch.Trim()
Assert-BranchNamingPolicy -name $Branch

if (-not $NoFetch) {
  Write-Host "Fetching '$Remote'..."
  git fetch $Remote
  if ($LASTEXITCODE -ne 0) { throw "git fetch failed for remote '$Remote'." }
}

$worktreePath = Get-WorktreePath -repoRoot $repoRoot -name $Branch

if (Test-Path -LiteralPath $worktreePath) {
  Write-Host "Worktree already exists: $worktreePath"
} else {
  Write-Host "Creating worktree for '$Branch'..."
  Ensure-ParentDirectory -path $worktreePath

  if (Test-LocalBranchExists -name $Branch) {
    git worktree add $worktreePath $Branch
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for branch '$Branch'." }
  }
  elseif (Test-RemoteBranchExists -remoteName $Remote -name $Branch) {
    # Create a local branch so the worktree is NOT detached HEAD.
    git worktree add -b $Branch $worktreePath "$Remote/$Branch"
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for remote branch '$Remote/$Branch'." }

    # Best effort: set upstream so future pulls/pushes behave as expected.
    git branch --set-upstream-to="$Remote/$Branch" $Branch *> $null
  }
  else {
    git worktree add -b $Branch $worktreePath $startPoint
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for branch '$Branch'." }
  }
}

Write-Host "Worktree path: $worktreePath"

if (-not $NoCd) {
  Set-Location -LiteralPath $worktreePath
  Write-Host "Current directory: $worktreePath"
}


<#
.SYNOPSIS
  Creates a git worktree using the required worktree/branch convention.

.DESCRIPTION
  Policy enforced:
  - Each agent works in its own worktree + its own branch.
  - Branch prefix must be:
      feature/<topic>  (features/infra/refactor/docs)
      bugfix/<topic>   (only bugfixes)
  - Worktree path equals branch name under .worktrees/ ("/" becomes subfolder):
      feature/azure-iac-sql-infra -> .worktrees/feature/azure-iac-sql-infra

.PARAMETER Branch
  One or more branch names to create worktrees for.

.PARAMETER Base
  Base branch to branch off when creating a new branch (default: main).

.PARAMETER Remote
  Remote name used to resolve the base branch (default: origin).

.EXAMPLE
  ./scripts/new-worktree.ps1 -Branch feature/azure-iac-sql-infra

.EXAMPLE
  ./scripts/new-worktree.ps1 -Branch feature/azure-iac-sql-infra,feature/azure-iac-sql-app,feature/azure-iac-sql-deploy
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string[]] $Branch,

  [Parameter()]
  [string] $Base = "main",

  [Parameter()]
  [string] $Remote = "origin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-GitRepo {
  git rev-parse --is-inside-work-tree *> $null
  if ($LASTEXITCODE -ne 0) {
    throw "Not inside a git repository."
  }
}

function Get-RepoRoot {
  $root = (git rev-parse --show-toplevel).Trim()
  if (-not $root) { throw "Unable to determine git repository root." }
  return $root
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

function Assert-BranchNamingPolicy([string] $name) {
  # Required:
  # - must start with feature/ or bugfix/
  # - topic must be lowercase, digits, or hyphen, with optional subpaths
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

function Ensure-ParentDirectory([string] $path) {
  $parent = Split-Path -Parent $path
  if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
  }
}

Assert-GitRepo
$repoRoot = Get-RepoRoot
$startPoint = Resolve-StartPoint -remoteName $Remote -baseName $Base

# PowerShell's argument parsing can pass comma-separated values as a single string
# when using -File. Normalize to a flat list of branch names.
$expandedBranches = @()
foreach ($b in $Branch) {
  if ($null -eq $b) { continue }
  $expandedBranches += ($b -split ',')
}

foreach ($raw in $expandedBranches) {
  $name = $raw.Trim()
  if (-not $name) { continue }

  Assert-BranchNamingPolicy -name $name
  $worktreePath = Get-WorktreePath -repoRoot $repoRoot -name $name

  if (Test-Path -LiteralPath $worktreePath) {
    throw "Worktree path already exists: $worktreePath"
  }

  Ensure-ParentDirectory -path $worktreePath

  if (Test-LocalBranchExists -name $name) {
    Write-Host "Adding worktree for existing local branch '$name' -> $worktreePath"
    git worktree add $worktreePath $name
  }
  elseif (Test-RemoteBranchExists -remoteName $Remote -name $name) {
    Write-Host "Adding worktree for existing remote branch '$Remote/$name' -> $worktreePath"
    git worktree add $worktreePath "$Remote/$name"
  }
  else {
    Write-Host "Creating branch '$name' from '$startPoint' and adding worktree -> $worktreePath"
    git worktree add -b $name $worktreePath $startPoint
  }

  if ($LASTEXITCODE -ne 0) {
    throw "git worktree add failed for branch '$name'."
  }
}

Write-Host "Done."


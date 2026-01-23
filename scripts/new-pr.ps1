<#
.SYNOPSIS
  Commits current branch changes and creates a GitHub Pull Request.

.DESCRIPTION
  Uses GitHub CLI (gh) to create a PR for the current branch.
  Enforces the repository convention:
    - Branch must be feature/<topic> or bugfix/<topic>
    - Never create PRs from main

  Behavior:
    - Stages and commits changes if working tree is dirty
    - Pushes the current branch (sets upstream) when needed
    - Creates the PR with a standard body (Summary + Test plan)

.PARAMETER Base
  Base branch for the PR (default: main).

.PARAMETER Message
  Commit message. Required if there are uncommitted changes.

.PARAMETER NoCommit
  Skip committing and only create a PR (requires clean working tree).

.PARAMETER Title
  PR title. If omitted, derives a title from the branch name.

.PARAMETER Body
  PR body. If omitted, uses the default template (Summary + Test plan).

.PARAMETER Draft
  Create the PR as draft.

.PARAMETER Remote
  Remote name to push to (default: origin).

.EXAMPLE
  ./scripts/new-pr.ps1

.EXAMPLE
  ./scripts/new-pr.ps1 -Title "infra: add Azure SQL + App Service IaC" -Draft
#>

[CmdletBinding()]
param(
  [Parameter()]
  [string] $Base = "main",

  [Parameter()]
  [string] $Message,

  [Parameter()]
  [switch] $NoCommit,

  [Parameter()]
  [string] $Title,

  [Parameter()]
  [string] $Body,

  [Parameter()]
  [switch] $Draft,

  [Parameter()]
  [string] $Remote = "origin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ghExe = "gh"

function Assert-CommandExists([string] $cmd) {
  $cmdInfo = Get-Command $cmd -ErrorAction SilentlyContinue
  if (-not $cmdInfo) {
    throw "Required command '$cmd' not found. Install it and ensure it's on PATH."
  }
}

function Resolve-GhExe {
  $cmd = Get-Command "gh" -ErrorAction SilentlyContinue
  if ($cmd) { return "gh" }

  $fallback = "C:\Program Files\GitHub CLI\gh.exe"
  if (Test-Path -LiteralPath $fallback) { return $fallback }

  throw "Required command 'gh' not found. Install GitHub CLI and ensure it's on PATH (or at '$fallback')."
}

function Assert-GitRepo {
  git rev-parse --is-inside-work-tree *> $null
  if ($LASTEXITCODE -ne 0) { throw "Not inside a git repository." }
}

function Get-CurrentBranch {
  $b = (git rev-parse --abbrev-ref HEAD).Trim()
  if (-not $b) { throw "Unable to determine current branch." }
  return $b
}

function Assert-NotMain([string] $branch) {
  if ($branch -eq "main" -or $branch -eq "master") {
    throw "Refusing to create a PR from '$branch'. Create a feature/ or bugfix/ branch first."
  }
}

function Assert-BranchNamingPolicy([string] $name) {
  $pattern = '^(feature|bugfix)\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?(\/[a-z0-9]+([a-z0-9\-]*[a-z0-9]+)?)*$'
  if ($name -notmatch $pattern) {
    throw "Branch '$name' violates policy. Use feature/<topic> or bugfix/<topic> (lowercase, digits, hyphen; '/' allowed for subfolders)."
  }
}

function Assert-CleanWorkingTree {
  $status = Get-WorkingTreeStatus
  if ($status) {
    throw "Working tree is not clean. Commit or stash changes before creating a PR."
  }
}

function Get-WorkingTreeStatus {
  # Ensure null-safe behavior under StrictMode: a clean tree may produce no output ($null).
  $lines = @(git status --porcelain)
  return ($lines -join "`n").Trim()
}

function Ensure-CommitIfNeeded([string] $msg, [switch] $skipCommit) {
  $status = Get-WorkingTreeStatus

  if (-not $status) {
    return
  }

  if ($skipCommit) {
    throw "Working tree has uncommitted changes, but -NoCommit was set."
  }

  if (-not $msg) {
    throw 'Commit message is required when there are uncommitted changes. Provide -Message "...".'
  }

  Write-Host "Staging changes..."
  git add -A
  if ($LASTEXITCODE -ne 0) { throw "git add failed." }

  Write-Host "Committing..."
  git commit -m $msg
  if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
}

function Ensure-Upstream([string] $remoteName, [string] $branch) {
  git rev-parse --abbrev-ref --symbolic-full-name "@{u}" *> $null
  if ($LASTEXITCODE -eq 0) { return }

  Write-Host "No upstream set. Pushing '$branch' to '$remoteName' with upstream..."
  git push -u $remoteName HEAD
  if ($LASTEXITCODE -ne 0) { throw "git push failed." }
}

function Get-DefaultTitle([string] $branch) {
  # feature/azure-iac-sql-infra -> azure iac sql infra
  $t = $branch -replace '^(feature|bugfix)/', ''
  $t = ($t -replace '[/\\-]+', ' ').Trim()
  if (-not $t) { $t = $branch }
  return $t
}

function Get-DefaultBody {
  return @"
## Summary
- 

## Test plan
- [ ] 
"@
}

Assert-CommandExists -cmd "git"
$ghExe = Resolve-GhExe
Assert-GitRepo

$branch = Get-CurrentBranch
Assert-NotMain -branch $branch
Assert-BranchNamingPolicy -name $branch

Ensure-CommitIfNeeded -msg $Message -skipCommit:$NoCommit
if ($NoCommit) { Assert-CleanWorkingTree }
Ensure-Upstream -remoteName $Remote -branch $branch

if (-not $Title) { $Title = Get-DefaultTitle -branch $branch }
if (-not $Body) { $Body = Get-DefaultBody }

$draftArg = @()
if ($Draft) { $draftArg = @("--draft") }

Write-Host "Creating PR from '$branch' -> '$Base'..."

& $ghExe pr create `
  --base $Base `
  --head $branch `
  --title $Title `
  --body $Body `
  @draftArg

if ($LASTEXITCODE -ne 0) {
  throw "gh pr create failed."
}

Write-Host "Done."


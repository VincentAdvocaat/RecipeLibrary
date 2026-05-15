# Fetches with remote prune, then deletes local branches that:
# - have no upstream, or upstream is [gone] after fetch --prune
# - are fully merged into the default branch (origin/HEAD or main)
# - are not ahead of that branch (no unique commits)
#
# Install: .\scripts\install-git-fetch-prune-local-alias.ps1
# Usage:   git fetch-prune-local

$ErrorActionPreference = 'Stop'

function Get-DefaultBranch {
    $originHead = git symbolic-ref --quiet refs/remotes/origin/HEAD 2>$null
    if ($originHead) {
        return ($originHead -replace '^refs/remotes/origin/', '')
    }
    foreach ($name in @('main', 'master')) {
        if (git show-ref --verify --quiet "refs/heads/$name") { return $name }
    }
    throw 'Could not determine default branch (main/master).'
}

function Test-BranchFullyMerged([string]$Branch, [string]$Base) {
    if ($Branch -eq $Base) { return $false }
    $null = git merge-base --is-ancestor $Branch $Base 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    $ahead = [int](git rev-list --count "$Base..$Branch" 2>$null)
    return $ahead -eq 0
}

function Get-BranchUpstreamGone([string]$Branch) {
    $line = git branch -vv $Branch 2>$null
    return $line -match ': gone\]'
}

function Test-BranchHasUpstream([string]$Branch) {
    git rev-parse --verify --quiet "$Branch@{upstream}" 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error 'Not inside a git repository.'
    exit 1
}
Set-Location $repoRoot

Write-Host 'Fetching with --prune...' -ForegroundColor Cyan
git fetch --prune
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$defaultBranch = Get-DefaultBranch
$current = git branch --show-current
$deleted = [System.Collections.Generic.List[string]]::new()
$skipped = [System.Collections.Generic.List[string]]::new()

foreach ($branch in (git for-each-ref --format='%(refname:short)' refs/heads)) {
    if ($branch -eq $current) { continue }

    $hasUpstream = Test-BranchHasUpstream $branch
    $upstreamGone = Get-BranchUpstreamGone $branch
    $noUpstream = -not $hasUpstream

    if (-not ($noUpstream -or $upstreamGone)) {
        continue
    }

    if (-not (Test-BranchFullyMerged $branch $defaultBranch)) {
        $reason = if ($noUpstream) { 'no upstream' } else { 'upstream gone' }
        $skipped.Add("$branch ($reason, not fully merged into $defaultBranch)")
        continue
    }

    git branch -d $branch 2>&1 | Out-Host
    if ($LASTEXITCODE -eq 0) {
        $deleted.Add($branch)
    }
    else {
        $skipped.Add("$branch (delete refused)")
    }
}

Write-Host ''
if ($deleted.Count -gt 0) {
    Write-Host "Deleted $($deleted.Count) branch(es):" -ForegroundColor Green
    $deleted | ForEach-Object { Write-Host "  - $_" }
}
else {
    Write-Host 'No local branches deleted.' -ForegroundColor DarkGray
}

if ($skipped.Count -gt 0) {
    Write-Host ''
    Write-Host 'Skipped:' -ForegroundColor Yellow
    $skipped | ForEach-Object { Write-Host "  - $_" }
}

Write-Host ''
Write-Host 'Remaining branches:' -ForegroundColor Cyan
git branch -vv

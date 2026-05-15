# Registers: git fetch-prune-local  ->  scripts/git-fetch-prune-local.ps1
# Run once per machine (or after moving the repo).

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'git-fetch-prune-local.ps1'
if (-not (Test-Path $scriptPath)) {
    Write-Error "Script not found: $scriptPath"
    exit 1
}

$resolved = (Resolve-Path $scriptPath).Path -replace '\\', '/'
$aliasValue = "!powershell -NoProfile -ExecutionPolicy Bypass -File `"$resolved`""

git config --global alias.fetch-prune-local $aliasValue

Write-Host 'Installed global git alias:' -ForegroundColor Green
Write-Host '  git fetch-prune-local'
Write-Host ''
Write-Host 'From any repo, this will:' -ForegroundColor DarkGray
Write-Host '  1. git fetch --prune'
Write-Host '  2. delete local branches with no upstream or [gone] upstream'
Write-Host '     when they are fully merged into origin/HEAD (or main)'

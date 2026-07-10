<#
.SYNOPSIS
  Installs global PowerShell commands: rlstart and recipelibrary.

.DESCRIPTION
  Adds functions to your PowerShell profile that invoke scripts in this repository.
  Re-run from another checkout (e.g. a worktree) to point commands at that copy.

.PARAMETER RepoRoot
  Repository root to bind commands to. Defaults to the parent of the scripts folder.

.PARAMETER Uninstall
  Remove RecipeLibrary CLI functions from the profile.

.EXAMPLE
  ./scripts/install-cli.ps1
#>

[CmdletBinding()]
param(
  [Parameter()]
  [string] $RepoRoot = '',

  [Parameter()]
  [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
else {
  $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$profilePath = $PROFILE.CurrentUserAllHosts
$profileDir = Split-Path -Parent $profilePath
if (-not (Test-Path -LiteralPath $profileDir)) {
  New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
}
if (-not (Test-Path -LiteralPath $profilePath)) {
  New-Item -ItemType File -Path $profilePath -Force | Out-Null
}

$beginMarker = '# >>> RecipeLibrary CLI >>>'
$endMarker = '# <<< RecipeLibrary CLI <<<'

$block = @'
# >>> RecipeLibrary CLI >>>
$RecipeLibraryRoot = '__REPO_ROOT__'
function global:rlstart {
  & "$RecipeLibraryRoot\scripts\rlstart.ps1" @args
}
function global:recipelibrary {
  & "$RecipeLibraryRoot\scripts\recipelibrary.ps1" @args
}
# <<< RecipeLibrary CLI <<<
'@.Replace('__REPO_ROOT__', $RepoRoot.Replace("'", "''"))

$content = Get-Content -LiteralPath $profilePath -Raw -ErrorAction SilentlyContinue
if ($null -eq $content) { $content = '' }

$pattern = [regex]::Escape($beginMarker) + '[\s\S]*?' + [regex]::Escape($endMarker)
if ($Uninstall) {
  if ($content -match $pattern) {
    $content = [regex]::Replace($content, $pattern, '').TrimEnd()
    Set-Content -LiteralPath $profilePath -Value $content -Encoding utf8
    Write-Host "Removed RecipeLibrary CLI from $profilePath" -ForegroundColor Green
  }
  else {
    Write-Host 'RecipeLibrary CLI block not found in profile.' -ForegroundColor Yellow
  }
  return
}

if ($content -match $pattern) {
  $content = [regex]::Replace($content, $pattern, $block.TrimEnd())
}
else {
  if ($content.Length -gt 0 -and -not $content.EndsWith("`n")) {
    $content += "`n`n"
  }
  $content += $block
}

Set-Content -LiteralPath $profilePath -Value $content.TrimEnd() -Encoding utf8

Write-Host "Installed RecipeLibrary CLI in:" -ForegroundColor Green
Write-Host "  $profilePath"
Write-Host ''
Write-Host "Repository root: $RepoRoot"
Write-Host ''
Write-Host 'Restart PowerShell, then run:' -ForegroundColor Cyan
Write-Host '  rlstart'
Write-Host '  recipelibrary start'

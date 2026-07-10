<#
.SYNOPSIS
  Shortcut to start local RecipeLibrary (SQL + web app).

.EXAMPLE
  rlstart
  rlstart -SkipSql
#>

[CmdletBinding()]
param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [object[]] $RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$startScript = Join-Path $PSScriptRoot 'start-local.ps1'
& $startScript @RemainingArgs

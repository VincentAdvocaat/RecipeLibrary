<#
.SYNOPSIS
  RecipeLibrary command-line helper.

.DESCRIPTION
  Subcommands:
    start   Start local SQL + web app and print the frontend URL
    help    Show usage

.EXAMPLE
  recipelibrary start
  recipelibrary start -SkipSql
#>

[CmdletBinding()]
param(
  [Parameter(Position = 0)]
  [ValidateSet('start', 'help')]
  [string] $Command = 'help',

  [Parameter()]
  [int] $HttpPort = 5197,

  [Parameter()]
  [int] $HttpsPort = 5196,

  [Parameter()]
  [switch] $SkipSql,

  [Parameter()]
  [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$startScript = Join-Path $PSScriptRoot 'start-local.ps1'

switch ($Command) {
  'start' {
    & $startScript -HttpPort $HttpPort -HttpsPort $HttpsPort -SkipSql:$SkipSql -NoBuild:$NoBuild
  }
  default {
    Write-Host @'
RecipeLibrary CLI

Usage:
  recipelibrary start [options]
  rlstart              (alias)

Options (start):
  -HttpPort <port>     Default: 5197
  -HttpsPort <port>    Default: 5196
  -SkipSql             Do not start the Docker SQL container
  -NoBuild             Skip build when starting the web app

Examples:
  recipelibrary start
  recipelibrary start -SkipSql
  rlstart

Install global commands (PowerShell):
  ./scripts/install-cli.ps1

'@
  }
}

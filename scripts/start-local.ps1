<#
.SYNOPSIS
  Starts local SQL (Docker) and the RecipeLibrary web app, then prints the frontend URL.

.DESCRIPTION
  1. Ensures .env exists (copies from .env.example when missing)
  2. Starts the SQL container (docker compose up -d sql --wait)
  3. Runs the Blazor web app on fixed local ports
  4. Waits until HTTP responds and prints the URL

  Press Ctrl+C to stop the web app. The SQL container keeps running unless you stop it
  with: docker compose stop sql

.PARAMETER HttpPort
  HTTP port for the web app (default: 5197).

.PARAMETER HttpsPort
  HTTPS port for the web app (default: 5196).

.PARAMETER SkipSql
  Skip starting the SQL container (use when SQL is already running).

.PARAMETER NoBuild
  Pass --no-build to dotnet run.

.EXAMPLE
  ./scripts/start-local.ps1
#>

[CmdletBinding()]
param(
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

function Get-RepoRoot {
  return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

function Test-DockerAvailable {
  docker info *> $null
  if ($LASTEXITCODE -ne 0) {
    throw 'Docker is not running. Start Docker Desktop and try again.'
  }
}

function Ensure-EnvFile([string] $repoRoot) {
  $envPath = Join-Path $repoRoot '.env'
  $examplePath = Join-Path $repoRoot '.env.example'
  if (Test-Path -LiteralPath $envPath) {
    return
  }

  if (-not (Test-Path -LiteralPath $examplePath)) {
    throw "Missing .env and .env.example in '$repoRoot'."
  }

  Copy-Item -LiteralPath $examplePath -Destination $envPath
  Write-Host "Created .env from .env.example. Review MSSQL_SA_PASSWORD before shared use." -ForegroundColor Yellow
}

function Import-DotEnv([string] $repoRoot) {
  $envPath = Join-Path $repoRoot '.env'
  Get-Content -LiteralPath $envPath | ForEach-Object {
    $line = $_.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { return }
    $eq = $line.IndexOf('=')
    if ($eq -lt 1) { return }
    $name = $line.Substring(0, $eq).Trim()
    $value = $line.Substring($eq + 1).Trim()
    if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
      $value = $value.Substring(1, $value.Length - 2)
    }
    Set-Item -Path "Env:$name" -Value $value
  }
}

function Start-SqlContainer([string] $repoRoot) {
  Write-Host 'Starting SQL container...'
  Push-Location -LiteralPath $repoRoot
  try {
    docker compose up -d sql --wait
    if ($LASTEXITCODE -ne 0) {
      throw 'docker compose up failed. Check Docker and .env (MSSQL_SA_PASSWORD).'
    }
  }
  finally {
    Pop-Location
  }
  Write-Host 'SQL container is ready.' -ForegroundColor Green
}

function Wait-ForWebApp([string] $url, [int] $timeoutSeconds) {
  $deadline = [datetime]::UtcNow.AddSeconds($timeoutSeconds)
  while ([datetime]::UtcNow -lt $deadline) {
    try {
      $null = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
      return
    }
    catch {
      Start-Sleep -Seconds 1
    }
  }

  throw "Web app did not respond at $url within ${timeoutSeconds}s."
}

$repoRoot = Get-RepoRoot
$webProject = Join-Path $repoRoot 'src\Web\RecipeLibrary.Web\RecipeLibrary.Web.csproj'
if (-not (Test-Path -LiteralPath $webProject)) {
  throw "Web project not found at '$webProject'."
}

Ensure-EnvFile -repoRoot $repoRoot
Import-DotEnv -repoRoot $repoRoot

if (-not $SkipSql) {
  Test-DockerAvailable
  Start-SqlContainer -repoRoot $repoRoot
}

$httpUrl = "http://localhost:$HttpPort"
$httpsUrl = "https://localhost:$HttpsPort"
$urls = "$httpsUrl;$httpUrl"

$dotnetArgs = @(
  'run',
  '--project', $webProject,
  '--urls', $urls,
  '--no-launch-profile'
)
if ($NoBuild) {
  $dotnetArgs += '--no-build'
}

Write-Host 'Starting RecipeLibrary web app...'

$process = Start-Process -FilePath 'dotnet' `
  -ArgumentList $dotnetArgs `
  -WorkingDirectory $repoRoot `
  -PassThru `
  -NoNewWindow

try {
  Wait-ForWebApp -url $httpUrl -timeoutSeconds 120

  Write-Host ''
  Write-Host 'RecipeLibrary is ready.' -ForegroundColor Green
  Write-Host "  Frontend (HTTP):  $httpUrl" -ForegroundColor Cyan
  Write-Host "  Frontend (HTTPS): $httpsUrl" -ForegroundColor Cyan
  Write-Host ''
  Write-Host 'Press Ctrl+C to stop the web app.' -ForegroundColor DarkGray

  Wait-Process -Id $process.Id
}
finally {
  if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  }
}

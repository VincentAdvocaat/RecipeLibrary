<#
.SYNOPSIS
  Installs Docker Desktop (and WSL2 prerequisites) for local development.

.DESCRIPTION
  Docker Desktop on Windows typically requires:
    - WSL2 + Virtual Machine Platform features
    - A reboot after enabling Windows features
    - Administrator privileges

  This script self-elevates and then attempts:
    - Enable required Windows optional features (no restart)
    - Install WSL (best-effort)
    - Install Docker Desktop via winget (silent)

  Notes:
    - You may still see a UAC prompt (expected).
    - A reboot is often required before Docker works.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsAdmin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
  Write-Host "Elevating to Administrator..."
  $args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", "`"$PSCommandPath`""
  )
  Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $args
  exit 0
}

Write-Host "Enabling Windows features for WSL2..."
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart | Out-Host
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart | Out-Host

Write-Host "Attempting WSL install (best-effort)..."
try {
  wsl.exe --install --no-distribution | Out-Host
} catch {
  Write-Warning "WSL install did not complete: $($_.Exception.Message)"
}

Write-Host "Installing Docker Desktop (silent) via winget..."
winget install -e --id Docker.DockerDesktop `
  --accept-package-agreements --accept-source-agreements `
  --silent --disable-interactivity | Out-Host

Write-Host ""
Write-Host "Done. If Docker doesn't work yet, reboot Windows and start Docker Desktop once."


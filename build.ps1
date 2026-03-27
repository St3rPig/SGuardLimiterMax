#Requires -Version 5.1
<#
.SYNOPSIS
    Builds SGuardLimiterMax in two flavours:
      standalone  — self-contained, ~150 MB, no runtime required
      framework   — framework-dependent, ~2 MB, needs .NET 8 Desktop Runtime

.USAGE
    .\build.ps1              # build both
    .\build.ps1 -Profile Standalone
    .\build.ps1 -Profile Framework
#>

param(
    [ValidateSet("Standalone", "Framework", "Both")]
    [string]$Profile = "Both"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -- Locate dotnet.exe --------------------------------------------------------─
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnet = $dotnetCmd.Source
} else {
    $dotnet = "$env:ProgramFiles\dotnet\dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        Write-Host "[ERROR] dotnet.exe not found. Install .NET 8 SDK from https://dot.net" -ForegroundColor Red
        exit 1
    }
}

$project  = Join-Path $PSScriptRoot "SGuardLimiterMax.csproj"
$profiles = @()
if ($Profile -eq "Both") { $profiles = @("Standalone", "Framework") }
else                      { $profiles = @($Profile) }

# -- Build each profile --------------------------------------------------------
$results = @()

foreach ($p in $profiles) {
    $outDir = Join-Path $PSScriptRoot "publish\$($p.ToLower())"

    Write-Host ""
    Write-Host "-- Building: $p ------------------------------------------" -ForegroundColor Cyan

    # Clean previous output
    if (Test-Path $outDir) {
        Remove-Item $outDir -Recurse -Force
    }

    $args = @(
        "publish", $project,
        "-c", "Release",
        "-p:PublishProfile=$p",
        "--nologo"
    )

    & $dotnet @args
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host "[FAIL] $p build failed (exit $exitCode)" -ForegroundColor Red
        $results += [PSCustomObject]@{ Profile = $p; Status = "FAIL"; Size = "-" }
        continue
    }

    $exe = Join-Path $outDir "SGuardLimiterMax.exe"
    if (Test-Path $exe) {
        $mb   = "{0:N1} MB" -f ((Get-Item $exe).Length / 1MB)
        $results += [PSCustomObject]@{ Profile = $p; Status = "OK"; Size = $mb }
        Write-Host "[OK]   $p → $exe  ($mb)" -ForegroundColor Green
    } else {
        $results += [PSCustomObject]@{ Profile = $p; Status = "MISSING"; Size = "-" }
        Write-Host "[WARN] $p build succeeded but exe not found at expected path." -ForegroundColor Yellow
    }
}

# -- Summary ------------------------------------------------------------------─
Write-Host ""
Write-Host "-- Summary --------------------------------------------------─" -ForegroundColor Cyan
$results | Format-Table -AutoSize

$failed = $results | Where-Object { $_.Status -ne "OK" }
if ($failed) { exit 1 } else { exit 0 }

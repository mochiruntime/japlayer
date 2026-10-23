[CmdletBinding()]
param (
    [ValidateSet('All', 'Unit', 'UI')]
    [string]$Suite = 'All',

    [switch]$NoBuild,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptRoot

Set-Location $SolutionRoot

if (-not $NoBuild -and -not $List) {
    Write-Host "==> Building solution..." -ForegroundColor Cyan
    dotnet build /p:ForceNoColor=true -m:1 /nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

$FailedSuites = @()

function Get-LatestExecutablePath([string]$ProjectName) {
    $DebugPath = Join-Path $SolutionRoot "tests\$ProjectName\bin\Debug\net8.0-windows10.0.19041.0\$ProjectName.exe"
    $X64Path = Join-Path $SolutionRoot "tests\$ProjectName\bin\x64\Debug\net8.0-windows10.0.19041.0\$ProjectName.exe"
    $Candidates = @($DebugPath, $X64Path)
    return $Candidates | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
}

$UnitTestExecutable = Get-LatestExecutablePath -ProjectName "Japlayer.UnitTests"
$UITestExecutable = Get-LatestExecutablePath -ProjectName "Japlayer.UITests"

if ($List) {
    if ($Suite -in @('All', 'Unit') -and $UnitTestExecutable) {
        Write-Host "`n--- Japlayer.UnitTests (Available Tests) ---" -ForegroundColor Yellow
        & $UnitTestExecutable --list-tests
    }
    if ($Suite -in @('All', 'UI') -and $UITestExecutable) {
        Write-Host "`n--- Japlayer.UITests (Available Tests) ---" -ForegroundColor Yellow
        & $UITestExecutable --list-tests
    }
    exit 0
}

# 1. Run Unit Tests
if ($Suite -in @('All', 'Unit')) {
    Write-Host "`n==> Running Unit Tests (Japlayer.UnitTests: 40 tests)..." -ForegroundColor Cyan
    if (-not $UnitTestExecutable) {
        Write-Error "Could not find Japlayer.UnitTests.exe. Please build the solution first."
        exit 1
    }

    & $UnitTestExecutable
    if ($LASTEXITCODE -ne 0) {
        $FailedSuites += "UnitTests"
    }
}

# 2. Run UI Tests
if ($Suite -in @('All', 'UI')) {
    Write-Host "`n==> Running UI Automation Tests (Japlayer.UITests: 7 tests)..." -ForegroundColor Cyan
    if (-not $UITestExecutable) {
        Write-Error "Could not find Japlayer.UITests.exe. Please build the solution first."
        exit 1
    }

    & $UITestExecutable
    if ($LASTEXITCODE -ne 0) {
        $FailedSuites += "UITests"
    }
}

Write-Host "`n========================================================" -ForegroundColor Cyan
if ($FailedSuites.Count -eq 0) {
    Write-Host "All requested test suites passed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "The following test suites failed: $($FailedSuites -join ', ')" -ForegroundColor Red
    exit 1
}

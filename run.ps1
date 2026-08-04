<#
.SYNOPSIS
    Starts the Help Desk Web API and the MVC front end together.

.EXAMPLE
    .\run.ps1
    .\run.ps1 -ApiPort 5285 -WebPort 5185
#>
param(
    [int]$ApiPort = 5285,
    [int]$WebPort = 5185,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "Help Desk Management System" -ForegroundColor Cyan
Write-Host "Rajrup Roy Chowdhury - IN26010404`n" -ForegroundColor DarkGray

if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build "$root\HelpDesk.sln" -c Debug --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

$apiUrl = "http://localhost:$ApiPort"
$webUrl = "http://localhost:$WebPort"

# Child processes inherit this session's environment. Setting the variables here rather than
# via Start-Process -Environment keeps the script working on Windows PowerShell 5.1, which
# does not have that parameter.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $apiUrl

Write-Host "Starting API on $apiUrl ..." -ForegroundColor Yellow
$api = Start-Process dotnet `
    -ArgumentList @('run', '--project', "$root\src\HelpDesk.Api", '--no-launch-profile', '--no-build') `
    -PassThru

# Wait for the API to answer before starting the UI, so the first page load is never an error page.
$ready = $false
foreach ($i in 1..40) {
    Start-Sleep -Milliseconds 500
    try {
        if ((Invoke-WebRequest "$apiUrl/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) {
            $ready = $true; break
        }
    } catch { }
}
if (-not $ready) { Write-Warning "API did not report healthy in time - starting the UI anyway." }

$env:ASPNETCORE_URLS = $webUrl
$env:ApiSettings__BaseUrl = "$apiUrl/"

Write-Host "Starting web UI on $webUrl ..." -ForegroundColor Yellow
$web = Start-Process dotnet `
    -ArgumentList @('run', '--project', "$root\src\HelpDesk.Mvc", '--no-launch-profile', '--no-build') `
    -PassThru

Start-Sleep -Seconds 3
Start-Process $webUrl

Write-Host "`n  UI       $webUrl"      -ForegroundColor Green
Write-Host "  API docs $apiUrl/swagger" -ForegroundColor Green
Write-Host "  Health   $apiUrl/health`n" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop both processes." -ForegroundColor DarkGray

try {
    Wait-Process -Id $api.Id, $web.Id
} finally {
    foreach ($p in @($api, $web)) {
        if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    }
}

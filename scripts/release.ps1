param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$SimConnectDll = "",
    [string]$GitHubToken = "",
    [switch]$Manual
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$prodDir = Join-Path $projectRoot "dist\PCareer.Client-prod"
$vpkOutput = Join-Path $projectRoot "dist\velopack-releases"

$token = $GitHubToken
if (-not $token) {
    $token = $env:VPNET_GITHUB_TOKEN
}
if (-not $token) {
    $token = $env:GITHUB_TOKEN
}

# Resolve VPK tool
$vpk = (Get-Command vpk -ErrorAction SilentlyContinue).Source
if (-not $vpk) {
    throw "vpk CLI not found. Install it with: dotnet tool install -g vpk"
}

# 1. Build production multi-file output
Write-Host "Building v$Version production package..." -ForegroundColor Cyan
& "$projectRoot\scripts\build_prod.ps1" -SimConnectDll $SimConnectDll -Version $Version
if ($LASTEXITCODE -ne 0) {
    throw "Production build failed"
}

# 2. Clean VPK output directory
if (Test-Path -LiteralPath $vpkOutput) {
    Remove-Item -LiteralPath $vpkOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $vpkOutput -Force | Out-Null

# 3. Pack with Velopack
Write-Host "Packing release with Velopack v$Version..." -ForegroundColor Cyan
$exePath = Join-Path $prodDir "VirtualPilotNetwork.exe"
& $vpk pack `
    --packId "VirtualPilotNetwork" `
    --packVersion $Version `
    --packDir $prodDir `
    --mainExe "VirtualPilotNetwork.exe" `
    --outputDir $vpkOutput
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed"
}

# 4. Upload to GitHub Releases
if ($Manual) {
    Write-Host ""
    Write-Host "Release files packed to: $vpkOutput" -ForegroundColor Green
    Write-Host "Upload them manually to GitHub Releases:" -ForegroundColor Yellow
    Write-Host "  1. Go to https://github.com/AnomalyCo/pcareer-desktop/releases/new"
    Write-Host "  2. Create tag: v$Version"
    Write-Host "  3. Upload all files from: $vpkOutput"
    Write-Host "  4. Publish the release"
} elseif ($token) {
    Write-Host "Uploading v$Version to GitHub Releases..." -ForegroundColor Cyan
    & $vpk upload github `
        --tag "v$Version" `
        --repoUrl "https://github.com/AnomalyCo/pcareer-desktop" `
        --token $token `
        --outputDir $vpkOutput
    if ($LASTEXITCODE -ne 0) {
        throw "vpk upload github failed"
    }
    Write-Host "Release v$Version uploaded successfully!" -ForegroundColor Green
} else {
    Write-Warning "No GitHub token provided. Skipping upload."
    Write-Warning "Set VPNET_GITHUB_TOKEN or GITHUB_TOKEN, or pass -GitHubToken."
    Write-Host "To upload manually:"
    Write-Host "  vpk upload github --tag v$Version --repoUrl https://github.com/AnomalyCo/pcareer-desktop --token <TOKEN> --outputDir $vpkOutput"
}

Write-Host "Release v$Version complete." -ForegroundColor Green

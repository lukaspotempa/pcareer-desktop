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
$releaseDir = Join-Path $projectRoot "dist\portable-release"
$repository = "lukaspotempa/pcareer-desktop"
$tag = "v$Version"

$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion) -or $parsedVersion.Build -lt 0) {
    throw "Version must contain at least major, minor, and patch numbers, for example 1.2.3."
}

$token = $GitHubToken
if (-not $token) { $token = $env:VPNET_GITHUB_TOKEN }
if (-not $token) { $token = $env:GITHUB_TOKEN }

Write-Host "Building portable v$Version release..." -ForegroundColor Cyan
& "$projectRoot\scripts\build_prod.ps1" -SimConnectDll $SimConnectDll -Version $Version
if ($LASTEXITCODE -ne 0) {
    throw "Production build failed"
}

if (Test-Path -LiteralPath $releaseDir) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

$sourceExe = Join-Path $prodDir "VirtualPilotNetwork.exe"
$releaseExe = Join-Path $releaseDir "VirtualPilotNetwork.exe"
$manifestPath = Join-Path $releaseDir "VirtualPilotNetwork-update.json"
Copy-Item -LiteralPath $sourceExe -Destination $releaseExe

$hash = (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item -LiteralPath $releaseExe).Length
$downloadUrl = "https://github.com/$repository/releases/download/$tag/VirtualPilotNetwork.exe"
$manifest = [ordered]@{
    version = $Version
    url = $downloadUrl
    sha256 = $hash
    size = $size
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Portable release created:" -ForegroundColor Green
Write-Host "  $releaseExe"
Write-Host "  $manifestPath"

if ($Manual -or -not $token) {
    if (-not $Manual) {
        Write-Warning "No GitHub token was provided, so the release was not uploaded."
    }
    Write-Host "Create GitHub release $tag and upload both files from:" -ForegroundColor Yellow
    Write-Host "  $releaseDir"
    return
}

$headers = @{
    Accept = "application/vnd.github+json"
    Authorization = "Bearer $token"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent" = "VirtualPilotNetwork-ReleaseScript"
}
$apiBase = "https://api.github.com/repos/$repository"

$createdDraft = $false
try {
    $release = Invoke-RestMethod -Uri "$apiBase/releases/tags/$tag" -Headers $headers -Method Get
}
catch {
    $statusCode = [int]$_.Exception.Response.StatusCode
    if ($statusCode -ne 404) { throw }
    $body = @{
        tag_name = $tag
        name = $tag
        draft = $true
        prerelease = $false
    } | ConvertTo-Json
    $release = Invoke-RestMethod `
        -Uri "$apiBase/releases" `
        -Headers $headers `
        -Method Post `
        -ContentType "application/json" `
        -Body $body
    $createdDraft = $true
}

$uploadBase = $release.upload_url -replace '\{\?name,label\}$', ''
foreach ($assetPath in @($releaseExe, $manifestPath)) {
    $assetName = Split-Path -Leaf $assetPath
    $existing = @($release.assets | Where-Object { $_.name -eq $assetName })
    foreach ($asset in $existing) {
        Invoke-RestMethod -Uri "$apiBase/releases/assets/$($asset.id)" -Headers $headers -Method Delete | Out-Null
    }

    $encodedName = [Uri]::EscapeDataString($assetName)
    Invoke-RestMethod `
        -Uri "$uploadBase`?name=$encodedName" `
        -Headers $headers `
        -Method Post `
        -ContentType "application/octet-stream" `
        -InFile $assetPath | Out-Null
    Write-Host "Uploaded $assetName" -ForegroundColor Green
}

if ($createdDraft) {
    $publishBody = @{ draft = $false } | ConvertTo-Json
    Invoke-RestMethod `
        -Uri "$apiBase/releases/$($release.id)" `
        -Headers $headers `
        -Method Patch `
        -ContentType "application/json" `
        -Body $publishBody | Out-Null
}

Write-Host "Portable release $tag uploaded successfully." -ForegroundColor Green

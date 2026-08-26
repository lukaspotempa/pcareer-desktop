param(
    [string]$SimConnectDll = "",
    [switch]$RequireSimConnect,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\PCareer.Client\PCareer.Client.csproj"
$outputDirectory = Join-Path $projectRoot "dist\PCareer.Client"

if (-not $SimConnectDll -and $env:MSFS2024_SDK) {
    $SimConnectDll = Join-Path $env:MSFS2024_SDK "SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll"
}

if (-not $SimConnectDll -and $env:MSFS_SDK) {
    $SimConnectDll = Join-Path $env:MSFS_SDK "SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll"
}

if (-not $SimConnectDll) {
    $candidatePaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Flight Simulator SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll"),
        (Join-Path $env:ProgramFiles "Microsoft Flight Simulator SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll")
    )
    $SimConnectDll = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ($SimConnectDll) {
    $SimConnectDll = [System.IO.Path]::GetFullPath($SimConnectDll)
    if (-not (Test-Path -LiteralPath $SimConnectDll -PathType Leaf)) {
        throw "Managed SimConnect assembly not found at: $SimConnectDll"
    }
}

if ($RequireSimConnect -and -not $SimConnectDll) {
    throw "SimConnect was required but no assembly was found. Pass -SimConnectDll or set MSFS2024_SDK."
}

if ($SimConnectDll) {
    Write-Host "Building with official SimConnect support: $SimConnectDll" -ForegroundColor Green
}
else {
    Write-Warning "MSFS 2024 SDK not found. Building the runnable UI fallback without simulator connectivity."
    Write-Warning "Install the SDK, set MSFS2024_SDK, and rebuild to enable SimConnect."
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$publishArguments = @(
    "publish",
    $projectFile,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", $selfContainedValue,
    "--output", $outputDirectory
)

if ($SimConnectDll) {
    $publishArguments += "-p:SimConnectAssemblyPath=$SimConnectDll"
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if ($SimConnectDll) {
    Copy-Item -LiteralPath $SimConnectDll -Destination (Join-Path $outputDirectory "Microsoft.FlightSimulator.SimConnect.dll") -Force
    Write-Host "Copied managed SimConnect assembly." -ForegroundColor Green

    $nativeDll = Join-Path (Split-Path -Parent (Split-Path -Parent $SimConnectDll)) "SimConnect.dll"
    if (Test-Path -LiteralPath $nativeDll -PathType Leaf) {
        Copy-Item -LiteralPath $nativeDll -Destination (Join-Path $outputDirectory "SimConnect.dll") -Force
        Write-Host "Copied native SimConnect runtime." -ForegroundColor Green
    }
    else {
        Write-Warning "Native SimConnect.dll was not found beside the SDK libraries. The SDK/GAC runtime must be installed on the target PC."
    }
}

$executable = Join-Path $outputDirectory "VirtualPilotNetwork.exe"
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Publish completed but the executable was not found: $executable"
}

Write-Host "Desktop client built successfully:" -ForegroundColor Green
Write-Host $executable

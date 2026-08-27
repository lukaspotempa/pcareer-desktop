param(
    [string]$SimConnectDll = "",
    [switch]$RequireSimConnect,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectFile = Join-Path $projectRoot "src\PCareer.Client\PCareer.Client.csproj"
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "dist\PCareer.Client"))

if (-not $outputDirectory.StartsWith(
    $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar,
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an output directory outside the repository: $outputDirectory"
}

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
    "--output", $outputDirectory,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:IncludeAllContentForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:PublishTrimmed=false"
)

if ($SimConnectDll) {
    $publishArguments += "-p:SimConnectAssemblyPath=$SimConnectDll"
    $nativeDll = Join-Path (Split-Path -Parent (Split-Path -Parent $SimConnectDll)) "SimConnect.dll"
    if (Test-Path -LiteralPath $nativeDll -PathType Leaf) {
        $publishArguments += "-p:SimConnectNativePath=$nativeDll"
    }
    elseif ($RequireSimConnect) {
        throw "Native SimConnect runtime not found at: $nativeDll"
    }
    else {
        Write-Warning "Native SimConnect.dll was not found. The SDK/GAC runtime must be installed on the target PC."
    }
}

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Get-ChildItem -Path $outputDirectory -Filter "*.xml" -File | Remove-Item -Force
Get-ChildItem -Path $outputDirectory -Filter "*.pdb" -File | Remove-Item -Force

$executable = Join-Path $outputDirectory "VirtualPilotNetwork.exe"
$publishedFiles = @(Get-ChildItem -LiteralPath $outputDirectory -File -Recurse)
if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].FullName -ne $executable) {
    $unexpectedFiles = ($publishedFiles.FullName -join ", ")
    throw "Single-file verification failed. Published files: $unexpectedFiles"
}

Write-Host "Desktop client built successfully:" -ForegroundColor Green
Write-Host $executable

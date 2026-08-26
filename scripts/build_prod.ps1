param(
    [string]$SimConnectDll = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectFile = Join-Path $projectRoot "src\PCareer.Client\PCareer.Client.csproj"
$outputDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $projectRoot "dist\PCareer.Client-prod"))

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
    $SimConnectDll = $candidatePaths |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if (-not $SimConnectDll) {
    throw "The MSFS SimConnect SDK was not found. Pass -SimConnectDll or set MSFS2024_SDK."
}

$SimConnectDll = [System.IO.Path]::GetFullPath($SimConnectDll)
if (-not (Test-Path -LiteralPath $SimConnectDll -PathType Leaf)) {
    throw "Managed SimConnect assembly not found at: $SimConnectDll"
}

$nativeSimConnectDll = Join-Path `
    (Split-Path -Parent (Split-Path -Parent $SimConnectDll)) `
    "SimConnect.dll"
if (-not (Test-Path -LiteralPath $nativeSimConnectDll -PathType Leaf)) {
    throw "Native SimConnect runtime not found at: $nativeSimConnectDll"
}
$nativeSimConnectDll = [System.IO.Path]::GetFullPath($nativeSimConnectDll)

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

Write-Host "Building single-file production client with SimConnect support..." -ForegroundColor Green
& dotnet publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputDirectory `
    "-p:SimConnectAssemblyPath=$SimConnectDll" `
    "-p:SimConnectNativePath=$nativeSimConnectDll" `
    "-p:PublishSingleFile=true" `
    "-p:IncludeNativeLibrariesForSelfExtract=true" `
    "-p:IncludeAllContentForSelfExtract=true" `
    "-p:EnableCompressionInSingleFile=true" `
    "-p:PublishTrimmed=false" `
    "-p:DebugSymbols=false" `
    "-p:DebugType=None"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedFiles = @(Get-ChildItem -LiteralPath $outputDirectory -File)
$executable = Join-Path $outputDirectory "PCareer.Client.exe"
if ($publishedFiles.Count -ne 1 -or
    $publishedFiles[0].FullName -ne $executable) {
    $unexpectedFiles = ($publishedFiles.Name -join ", ")
    throw "Single-file verification failed. Published files: $unexpectedFiles"
}

$sizeMegabytes = [Math]::Round($publishedFiles[0].Length / 1MB, 1)
Write-Host "Production client built successfully as one file:" -ForegroundColor Green
Write-Host "$executable ($sizeMegabytes MB)"

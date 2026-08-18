$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$stubProject = Join-Path $projectRoot "tests\SimConnect.CompileStub\SimConnect.CompileStub.csproj"
$clientProject = Join-Path $projectRoot "src\PCareer.Client\PCareer.Client.csproj"
$logicProject = Join-Path $projectRoot "tests\PCareer.Client.LogicTests\PCareer.Client.LogicTests.csproj"
$stubAssembly = Join-Path $projectRoot "tests\SimConnect.CompileStub\bin\Release\net8.0\Microsoft.FlightSimulator.SimConnect.dll"

& dotnet build $stubProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "SimConnect compile stub failed with exit code $LASTEXITCODE"
}

& dotnet build $clientProject --configuration Release "-p:SimConnectAssemblyPath=$stubAssembly"
if ($LASTEXITCODE -ne 0) {
    throw "SimConnect client compile check failed with exit code $LASTEXITCODE"
}

& dotnet run --project $logicProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Flight lifecycle checks failed with exit code $LASTEXITCODE"
}

Write-Host "All PCareer desktop checks passed." -ForegroundColor Green


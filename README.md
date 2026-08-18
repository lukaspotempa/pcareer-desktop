# PCareer Desktop Client

Windows companion application for the PCareer Microsoft Flight Simulator 2024
career platform. It connects to the user's simulator through the official
SimConnect SDK, displays live telemetry, checks whether a flight can start, and
tracks a local flight through takeoff, landing, and completion.

This repository contains only the desktop client. The future FastAPI server and
Vue web application belong in separate repositories.

## Current features

- Detects whether Microsoft Flight Simulator 2024 is available and retries every
  two seconds while it is closed.
- Reads aircraft, position, altitude, airspeed, heading, fuel, weight, gear,
  brakes, slew state, simulation rate, and ground state.
- Implements the local flight state machine:
  `Ready -> Started -> Airborne -> Landed -> Finished`.
- Validates basic start conditions such as on-ground state, 1x simulation rate,
  slew mode, required aircraft, and optional departure radius.
- Keeps future server communication behind `IFlightServerClient`.
- Produces a normal Windows executable with a PowerShell build script.

## Repository layout

```text
src/PCareer.Client/                WinForms application and SimConnect adapter
tests/PCareer.Client.LogicTests/   dependency-free flight lifecycle checks
tests/SimConnect.CompileStub/      compile-only managed API contract
scripts/build.ps1                  release publisher
scripts/test.ps1                   local verification
docs/ARCHITECTURE.md               client design and server boundary
```

The compile stub is never included in a release. Production builds reference
Microsoft's SDK assembly directly.

## Prerequisites

- Windows x64
- .NET 8 SDK or newer
- Microsoft Flight Simulator 2024 SDK

Install the SDK from MSFS 2024 Developer Mode using **Help -> SDK Installer**.
The build needs this file:

```text
<SDK>\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll
```

## Build

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
  -SimConnectDll "F:\FSSDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll" `
  -RequireSimConnect
```

The release is written to:

```text
dist\PCareer.Client\PCareer.Client.exe
```

Add `-SelfContained` if the target PC does not have the .NET 8 Desktop Runtime.
The build script copies both required SimConnect DLLs into the release folder.

Without `-RequireSimConnect`, the script can create a UI-only development build
when the SDK is unavailable. Such a build cannot connect to the simulator.

## Test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

This validates the flight lifecycle and compiles the SimConnect-specific source
without placing the compile stub in `dist`.

## Run

1. Start MSFS 2024 and load into a flight.
2. Run `dist\PCareer.Client\PCareer.Client.exe`.
3. Wait for the simulator status to become connected.
4. While on the ground at 1x simulation rate, click **Start flight**.
5. Take off and land, then click **Finish flight**.

The bundled development assignment permits any aircraft and airport. Later, the
server adapter will replace this with authenticated contract assignments and
batched telemetry uploads.

## License

No open-source license has been selected yet. Add a `LICENSE` file before making
the repository public if you want others to have explicit reuse rights.


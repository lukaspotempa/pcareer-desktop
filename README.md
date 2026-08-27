# Virtual Pilot Network Desktop

Windows companion application for the Virtual Pilot Network career platform and
Microsoft Flight Simulator 2024. It connects to MSFS through SimConnect,
authenticates through Discord, loads the player's active contract, and reports
flight telemetry to the Virtual Pilot Network server.

## Features

- Simulator connection and live aircraft telemetry
- Discord sign-in through the system browser
- Contract and aircraft validation before departure
- Local flight lifecycle and rule enforcement
- Portable, self-updating Windows executable
- No installer or automatically-created shortcuts

## Download

Download `VirtualPilotNetwork.exe` from the
[latest release](https://github.com/lukaspotempa/pcareer-desktop/releases/latest).
Keep it in a user-writable location so it can replace itself during updates.

Requirements: Windows x64, Microsoft Flight Simulator 2024, and the Microsoft
Edge WebView2 Runtime.

## Build

Building requires the .NET 8 SDK and the Microsoft Flight Simulator 2024 SDK.
Run this from the repository root:

```powershell
.\scripts\build.ps1 `
  -SimConnectDll "<MSFS SDK>\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll" `
  -RequireSimConnect `
  -SelfContained
```

The portable executable is written to
`dist\PCareer.Client\VirtualPilotNetwork.exe`.

Run all local checks with:

```powershell
.\scripts\test.ps1
```

## Release

Set a GitHub token with repository `Contents: read and write` permission, then
publish a version newer than the current release:

```powershell
$env:GITHUB_TOKEN = "<token>"

.\scripts\release.ps1 `
  -Version "0.0.3" `
  -SimConnectDll "<MSFS SDK>\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll"
```

The release contains the user-facing executable and a small checksum manifest
used by the automatic updater. The repository and release assets must remain
publicly accessible for unauthenticated update checks.

## Security

Please report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).
Never include access tokens, credentials, or private user data in a public issue.

## License

Copyright © 2026 Lukas Potempa. All rights reserved.

This source is publicly visible for review, but it is not open-source software.
Copying, modification, redistribution, sublicensing, and commercial use are not
permitted without prior written authorization. See [LICENSE](LICENSE).

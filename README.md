# Virtual Pilot Network Desktop

Virtual Pilot Network Desktop connects Microsoft Flight Simulator 2024 to the
Virtual Pilot Network career platform. It validates the selected aircraft and
departure, tracks the active flight, and reports flight progress to the server.

## Getting started

1. Download `VirtualPilotNetwork.exe` from the
   [latest release](https://github.com/lukaspotempa/pcareer-desktop/releases/latest).
2. Save it somewhere you can write to, such as a personal applications folder.
   This allows automatic updates to replace the executable.
3. Accept a contract on the
   [Virtual Pilot Network website](https://career.virtual-pilot.com/).
4. Start Microsoft Flight Simulator 2024 and load into the assigned aircraft at
   the departure airport.
5. Launch `VirtualPilotNetwork.exe` and sign in with Discord.
6. Wait for the simulator and contract checks to show ready, then select
   **Start flight**.
7. After landing at the destination, select **Finish flight**.

## Automatic updates

The application checks for updates when it starts. When a new version is
available, it downloads the replacement, verifies its SHA-256 checksum, updates
itself, and restarts. If the update service is temporarily unavailable, you can
continue offline or download the latest executable manually.

## Requirements

- Windows x64
- Microsoft Flight Simulator 2024
- Microsoft Edge WebView2 Runtime
- Internet access for sign-in, contracts, telemetry, and updates

## Troubleshooting

- **Simulator not connected:** Start MSFS 2024 and finish loading into a flight.
- **No active contract:** Accept a contract on the website, then refresh it in
  the desktop application.
- **WebView2 initialization error:** Install or repair the
  [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).
- **Update check failed:** Verify GitHub is reachable, continue offline, or
  download the latest release manually.

## License

Virtual Pilot Network Desktop is open-source software licensed under the
[Mozilla Public License 2.0](LICENSE). Changes to MPL-covered source files must
remain available under the MPL when distributed. Microsoft components used by
the application remain subject to their own license terms; see [NOTICE](NOTICE).

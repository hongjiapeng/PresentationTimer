# PresentationTimer

PresentationTimer is a local-first Windows presenter workspace built with .NET 10 and WinUI 3. It combines an accurate countdown/overtime timer, control and speaker-note monitoring for an already-running Microsoft PowerPoint slide show, and an authenticated phone browser remote over the local network.

## Prerequisites

- Windows 10 22H2 or Windows 11 on x64
- .NET 10 SDK
- Visual Studio 2026 with the WinUI application development workload
- Microsoft PowerPoint desktop, for PowerPoint integration checks
- Developer Mode enabled for unpackaged Debug launch

## Build and test

```powershell
dotnet restore PresentationTimer.sln
dotnet build PresentationTimer.sln -c Debug -p:Platform=x64
dotnet test PresentationTimer.sln -c Debug -p:Platform=x64
```

Run the desktop UI smoke checks after a Debug x64 build:

```powershell
.\scripts\ui-smoke.ps1 -AppPath .\src\PresentationTimer.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\PresentationTimer.App.exe
```

## Use

1. Open PowerPoint, open a deck, and start its slide show. PresentationTimer attaches automatically; it never starts or closes PowerPoint.
2. Enter a duration such as `15:00` and select **Start**. Pause, resume, and reset remain local desktop commands.
3. Select **Start remote**. Scan the displayed QR code from a phone on the same trusted Wi-Fi/LAN.
4. Use the browser's Previous/Next buttons and view the authoritative slide number, plain-text notes, and remaining/overtime value.
5. Select **End session** immediately after the talk. The QR token and every browser cookie from that session become invalid.

## Trusted-LAN security boundary

The MVP uses authenticated HTTP on the local network, not HTTPS. A 256-bit one-time pairing token is exchanged for a separate HttpOnly, SameSite=Strict browser cookie; credentials exist only in memory and are revoked at session end. This prevents accidental unauthenticated control, but it does not provide confidentiality against someone able to observe or alter traffic on hostile/public Wi-Fi. Use a trusted private network and end the session after presenting.

## Phone cannot connect

- Confirm the PC and phone are on the same Wi-Fi/LAN and can reach each other directly.
- Disable VPNs temporarily and check whether the access point enables client/AP isolation.
- In Windows Security, allow PresentationTimer on **Private** networks if Windows Firewall prompts. The app never elevates or changes firewall/network settings itself.
- If the PC changed networks or IP address, wait for replacement adapter choices, select the reachable URL, and scan its new QR code.
- A corporate device policy may block inbound LAN listeners; ask the administrator to permit this app for the local subnet.

When multiple operational adapters are available, select the labeled Ethernet, Wi-Fi, or other candidate whose subnet reaches the phone. If the PC address changes during a live session, PresentationTimer withdraws the stale QR, rebinds the host, and publishes replacement choices. Existing phones may reconnect when the endpoint remains reachable; otherwise scan the replacement QR.

## Local logs and privacy

PresentationTimer writes structured Serilog events to `%LOCALAPPDATA%\PresentationTimer\Logs`. Logs roll daily and at 5 MiB, retain at most seven files for seven days, and are flushed during normal shutdown. Speaker notes, pairing tokens, browser cookies, and complete token-bearing pairing URLs are deliberately excluded. Delete the log files only after closing the app if a dogfood troubleshooting record is no longer needed.

Application composition uses Microsoft.Extensions.DependencyInjection with constructor injection and build-time scope validation. The process owns a single timer, PowerPoint controller, remote host, and presentation-session service; window and page instances receive those services through DI rather than a global service locator.

## Portable publish

Create the unpackaged, self-contained x64 dogfood output with NativeAOT and trimming disabled:

```powershell
dotnet publish .\src\PresentationTimer.App\PresentationTimer.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -o .\artifacts\publish\win-x64
```

Launch `PresentationTimer.App.exe` directly from that directory. Uninstall/rollback is closing the app and deleting or replacing that portable directory; the MVP stores no presentation, token, or account data. Local diagnostic logs remain under `%LOCALAPPDATA%\PresentationTimer\Logs` unless removed separately. See [docs/manual-verification.md](docs/manual-verification.md) and the [PowerPoint checklist](docs/powerpoint-manual-checklist.md) before sharing a build.

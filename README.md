<div align="center">

[中文](./README.zh.md) · **English**

# PresentationTimer

**A local-first Windows presenter workspace for timing, PowerPoint control, speaker notes, and a phone remote.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![WinUI 3](https://img.shields.io/badge/UI-WinUI_3-0078D4?style=flat-square&logo=windows&logoColor=white)
![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square&logo=windows&logoColor=white)
[![Download](https://img.shields.io/badge/DOWNLOAD-GitHub_Releases-2ea44f?style=flat-square&labelColor=333)](https://github.com/hongjiapeng/PresentationTimer/releases)

</div>

PresentationTimer keeps the essential tools for a live talk in one place: an accurate countdown/overtime timer, PowerPoint navigation and speaker-note monitoring, plus an authenticated browser remote for a phone on the same trusted local network.

It runs locally, does not require an account, and does not close your presentation or PowerPoint when the app exits.

## Product tour

| Desktop workspace · design reference | Phone remote · live UI |
|:---:|:---:|
| <img src="docs/design/presentation-timer-expanded-ui-v2.png" alt="Expanded PresentationTimer desktop workspace design" width="610"> | <img src="docs/screenshots/phone-remote.png" alt="PresentationTimer phone remote showing timer, slide position, speaker notes, and navigation" width="255"> |
| Timer, PowerPoint status, slide controls, remote pairing, and duration settings in one workspace. | Remaining time, current slide, speaker notes, and Previous/Next controls in the phone browser. |

| Compact timer | Presenter HUD |
|:---:|:---:|
| <img src="docs/screenshots/compact-timer.png" alt="PresentationTimer compact timer" width="500"> | <img src="docs/screenshots/presenter-hud.png" alt="PresentationTimer presenter HUD" width="340"> |
| A focused timer with progress and essential controls. | A minimal always-visible surface for presenting. |

## What it does

| Area | Capability |
|---|---|
| **Timer** | Countdown, pause/resume, reset, progress, warning state, and overtime display |
| **PowerPoint** | Open or attach to a desktop presentation, track slide position and notes, and move Previous/Next |
| **Phone remote** | Pair by QR code and control slides from a browser on the same trusted Wi-Fi/LAN |
| **Local-first** | No account or cloud service; session credentials stay in memory and are revoked when the session ends |
| **Presentation modes** | Expanded workspace, compact timer, and presenter HUD |

## Quick start

1. Download the Windows installer or portable zip from [GitHub Releases](https://github.com/hongjiapeng/PresentationTimer/releases).
2. Open PresentationTimer and choose a duration such as `15:00`.
3. Open a `.ppt`, `.pptx`, `.pptm`, `.pps`, or `.ppsx` file, or start a slide show in PowerPoint first and let PresentationTimer attach automatically.
4. Start the timer. Pause, resume, and reset remain local desktop commands.
5. Select **Start remote**, then scan the QR code with a phone on the same trusted network.
6. After the talk, select **End session** to invalidate the QR token and every browser cookie from that session.

> [!IMPORTANT]
> The phone remote uses authenticated HTTP on the local network, not HTTPS. Use it only on a trusted private Wi-Fi/LAN. See [Security and privacy](#security-and-privacy).

## Requirements

| Requirement | Notes |
|---|---|
| Windows | Windows 10 22H2 or Windows 11 on x64 |
| PowerPoint | Microsoft PowerPoint desktop is required for presentation integration |
| Development | .NET 10 SDK, Visual Studio 2026 with the WinUI application development workload, and Developer Mode for unpackaged Debug launch |

PowerPoint integration requires the registered `PowerPoint.Application` COM class. WPS Office, PowerPoint for the web, and the Microsoft 365/Office portal app are not supported by the current adapter.

## Security and privacy

The remote session uses a 256-bit one-time pairing token, exchanged for a separate `HttpOnly`, `SameSite=Strict` browser cookie. Credentials exist only in memory and are revoked when the session ends.

This prevents accidental unauthenticated control, but HTTP does not provide confidentiality against someone able to observe or alter traffic on hostile or public Wi-Fi. Use a trusted private network and end the session after presenting.

Structured Serilog events are written to `%LOCALAPPDATA%\PresentationTimer\Logs`. Logs roll daily and at 5 MiB, retain at most seven files for seven days, and deliberately exclude speaker notes, pairing tokens, browser cookies, and complete token-bearing pairing URLs.

## Troubleshooting

<details>
<summary><strong>PowerPoint desktop is not detected</strong></summary>

Install or repair Microsoft PowerPoint desktop, start it once to complete sign-in or activation, and then restart PresentationTimer. Selecting a presentation file does not bypass the COM requirement.

The guided checker can open Microsoft's official installer page and recheck COM registration:

```powershell
.\scripts\Ensure-PowerPointDesktop.ps1 -OpenInstaller -WaitAfterOpening
```

If `winget` is available, Microsoft 365 Apps for enterprise can be installed with:

```powershell
winget install --id Microsoft.Office `
  --source winget `
  --accept-source-agreements `
  --accept-package-agreements
```

This installs the complete Microsoft 365 desktop suite. The script does not bypass sign-in, licensing, User Account Control, or Office activation.

</details>

<details>
<summary><strong>A selected PowerPoint file cannot be opened</strong></summary>

- Confirm that Microsoft PowerPoint desktop is installed and activated.
- Start PowerPoint once and finish any repair or activation prompts.
- Use a supported `.ppt`, `.pptx`, `.pptm`, `.pps`, or `.ppsx` file.
- WPS support requires a separate adapter and is not currently enabled.

Unsupported, missing, or unreadable files produce a safe error without changing the current presentation state.

</details>

<details>
<summary><strong>The phone cannot connect</strong></summary>

- Confirm that the PC and phone are on the same Wi-Fi/LAN and can reach each other directly.
- Temporarily disable VPNs and check whether the access point enables client/AP isolation.
- If Windows Firewall prompts, allow PresentationTimer on **Private** networks. The app never elevates or changes firewall settings itself.
- If the PC changes network or IP address, select the new reachable adapter URL and scan the replacement QR code.
- Corporate policy may block inbound LAN listeners; ask the administrator to permit the app for the local subnet.

</details>

## Development

### Build and test

```powershell
dotnet restore PresentationTimer.sln
dotnet build PresentationTimer.sln -c Debug -p:Platform=x64
dotnet test PresentationTimer.sln -c Debug -p:Platform=x64
```

Run the desktop UI smoke checks after a Debug x64 build:

```powershell
.\scripts\ui-smoke.ps1 -AppPath .\src\PresentationTimer.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\PresentationTimer.App.exe
```

### Portable publish

Create an unpackaged, self-contained x64 output with trimming disabled:

```powershell
dotnet publish .\src\PresentationTimer.App\PresentationTimer.App.csproj -c Release -p:Platform=x64 -r win-x64 -o .\artifacts\publish\win-x64
```

Launch `PresentationTimer.App.exe` from the publish directory. Removing the portable build only requires closing the app and deleting that directory; local diagnostic logs remain under `%LOCALAPPDATA%\PresentationTimer\Logs` unless removed separately.

### Windows installer and release

The Inno Setup installer installs per-user to `%LOCALAPPDATA%\Programs\PresentationTimer`, requires no administrator rights, supports English, Simplified Chinese, and Traditional Chinese, and can optionally create a desktop shortcut or start the app at sign-in.

Build it locally with Inno Setup 6 installed:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.0
```

To publish a release after committing and pushing the intended changes:

```powershell
.\scripts\release.ps1 0.1.0
```

The release script runs the test suite and pushes an annotated `v*` tag. GitHub Actions then builds the installer and portable zip and creates or updates the GitHub Release.

## PowerPoint test fixture

[`tests/fixtures/PresentationTimer.PowerPointFixture.pptx`](tests/fixtures/PresentationTimer.PowerPointFixture.pptx) is an original, programmatically authored six-slide deck for manual verification. It covers empty notes, multiline notes, markup-like text that must remain plain text, a genuinely hidden slide, and the final-slide boundary.

The deck contains no external images, charts, factual claims, or third-party assets. It is intended as a test fixture and manual-verification sample, not a product-demo deck. See the [manual verification guide](docs/manual-verification.md) and [PowerPoint checklist](docs/powerpoint-manual-checklist.md).

## Third-party notices

Third-party components and notices are documented in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

<div align="center">

Built for focused presentations on Windows.

</div>

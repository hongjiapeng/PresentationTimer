# Verification report — 2026-08-22

Environment: Windows x64, .NET SDK from `global.json`, local user session in Asia/Shanghai. Desktop PowerPoint automation reported unavailable on this machine, so no real-COM or physical-phone result is claimed.

## Automated results

| Check | Command / evidence | Result |
| --- | --- | --- |
| Full Debug tests | `dotnet test PresentationTimer.sln -c Debug -p:Platform=x64 --no-restore` | 76 passed: Core 50, Remote 23, App 3; 0 failed/skipped/warnings |
| Release solution build | `dotnet build PresentationTimer.sln -c Release -p:Platform=x64 --no-restore` | Passed; 0 warnings, 0 errors |
| Self-contained x64 publish | `dotnet publish .\src\PresentationTimer.App\PresentationTimer.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -o .\artifacts\publish\win-x64-20260822-serilog-di --no-restore` | Passed; 677 files, 316,823,474 bytes |
| Published UI smoke | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ui-smoke.ps1 -AppPath .\artifacts\publish\win-x64-20260822-serilog-di\PresentationTimer.App.exe` | 10 passed, 0 failed |
| Normal process shutdown | `%LOCALAPPDATA%\PresentationTimer\Logs\presentation-timer-20260822.json` | Recorded remote stop, PowerPoint monitoring stop, and `Application shutdown completed`; no force-kill evidence |
| OpenSpec strict validation | `openspec validate build-presentation-timer-mvp --type change --strict` | Valid |
| PowerPoint fixture rendering | bundled `render_slides.py`, `create_montage.py`, and `slides_test.py` | Six slides rendered and individually reviewed; no overflow |
| PowerPoint fixture structure | OOXML inspection | Six slides/notes pages; slide 5 has `show="0"`; empty/multiline/markup/hidden/final notes match the checklist |
| Test quality | assertion audit plus verified pseudo-mutations | 60 methods, 214 assertions, no assertion-free tests; 7/7 high-risk mutations killed and reverted |

## Published artifact contents

The output contains the application executable plus PresentationTimer Core/PowerPoint/Remote assemblies, self-contained .NET and Windows App SDK files, ASP.NET Core, Office interop, Microsoft DI, and Serilog. Phone assets are embedded in `PresentationTimer.Remote.dll` by design. The publish output is intentionally unpackaged and x64; NativeAOT and trimming remain disabled.

## Outstanding manual matrix

The following remain Pending, not Passed:

- 13-case real PowerPoint checklist on available 32-bit and 64-bit Office installations.
- iPhone Safari and Android Chrome portrait/landscape, reconnect, expired QR, and one-handed-use checks.
- Windows Firewall allowed/denied/managed-policy and physical LAN/IP-change paths.
- System light/dark/high-contrast visual checks and clean Windows user-profile launch.
- Full end-to-end phone → real PowerPoint navigation and repeated COM attach/detach/reference observation.

Use [powerpoint-manual-checklist.md](powerpoint-manual-checklist.md) to record those results without converting unavailable environments into passes.

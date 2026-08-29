## Why

Presenters currently have to coordinate a separate timer, PowerPoint controls, and speaker notes, while conventional clickers do not expose notes or remaining time and many remote-control products require an account, cloud service, or phone app. A lightweight, local-first Windows tool can turn any phone browser into a reliable presenter remote and provide an at-a-glance countdown for team knowledge sharing, technical talks, demos, training, and internal reports.

Windows-first is the smallest useful scope because Microsoft PowerPoint desktop automation is the product's defining integration and is available through Windows. A browser-based phone client avoids installation, store distribution, platform-specific mobile code, and Internet dependence while supporting both iPhone and Android on the same LAN.

## What Changes

- Add a simple WinUI desktop workspace centered on a large, resizable presentation countdown, with PowerPoint, remote-server, QR, and phone-connection status.
- Add an elapsed-time-based timer with start, pause, resume, reset, visible overtime, and authoritative state shared by desktop and phone clients.
- Add a user-initiated file picker that can open a supported presentation read-only in desktop Microsoft PowerPoint and start its slide show, while retaining read-only observation and next/previous control of an already-running slide show, including slide position and current speaker notes.
- Add a local-only remote session that publishes a scannable LAN URL, requires a fresh cryptographically secure session token, and invalidates the token when the session ends.
- Add a touch-first mobile web presenter showing slide position, speaker notes, remaining/overtime time, connection state, and large previous/next controls with automatic reconnect and state resynchronization.
- Add understandable degraded states and diagnostics for missing or restarted PowerPoint, no presentation or slide show, unavailable LAN addresses, port conflicts, Windows Firewall, phone disconnects, IP changes, invalid or expired tokens, and orderly shutdown.
- Add automated tests for pure timer/state/session behavior and the remote protocol, plus a focused manual PowerPoint COM integration checklist.
- Keep the MVP local-first and dependency-light. It does not add accounts, cloud services, Internet remote access, native mobile apps, PowerPoint editing or add-ins, thumbnails, drawing, analytics, presentation history, or providers other than PowerPoint.

## Capabilities

### New Capabilities

- `desktop-presenter-workspace`: The user-visible Windows workflow for configuring and operating a presentation, seeing authoritative status, and receiving actionable diagnostics.
- `presentation-timing`: Accurate countdown, pause/resume/reset, overtime, and timer-state behavior independent of UI refresh rate.
- `powerpoint-control`: User-initiated presentation opening, detection, observation, notes retrieval, navigation, event-driven synchronization, and graceful loss/recovery of a running PowerPoint slide show.
- `remote-session`: LAN server lifecycle, QR address selection, ephemeral token authorization, connection status, and session invalidation.
- `browser-presenter-remote`: Touch-first browser controls, authoritative presenter data, real-time updates, disconnect feedback, reconnect, and full state resynchronization.

### Modified Capabilities

None. This repository has no existing product capability specs.

## Impact

- Establishes a new .NET 10 Windows solution with separate desktop, core, PowerPoint integration, and remote-host responsibilities plus core and remote test projects.
- Introduces dependencies on WinUI 3/Windows App SDK, Microsoft Office PowerPoint interop, ASP.NET Core Kestrel and SignalR, and a local QR encoder.
- Opens one user-started HTTP listener on the local machine for the lifetime of a remote session; the application will not silently elevate or alter Windows Firewall settings.
- May activate desktop PowerPoint only after an explicit file selection, opens that presentation read-only, starts its slide show, and then observes and controls the active show without editing or taking shutdown ownership of the presentation; all COM subscriptions and references must be released on disconnect or application shutdown.
- Creates no database, cloud backend, account data, persistent credential, or native mobile application.

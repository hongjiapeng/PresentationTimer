## Why

The desktop application exposes the right presenter capabilities but gives persistent visual weight to subsystem status, diagnostics, and configuration. During a live talk the countdown must instead be the unmistakable focal point, with PowerPoint and phone controls available progressively without creating duplicate session state.

## What Changes

- Make a small, borderless Compact Timer the default desktop experience, with a draggable surface, optional always-on-top behavior, a dominant state-aware time display, and only the essential timer commands.
- Remove the redundant visible state caption from Compact mode while retaining localized, polite accessible state announcements for screen readers.
- Add an explicitly selected Presentation HUD for Running or Paused timers, keeping only the authoritative time and minimal commands visible so a full-screen slide is obscured as little as possible; Start itself stays in Compact and Reset from HUD restores the ready Compact surface.
- Replace transparent-looking and oversized timer controls with a solid semantic command hierarchy: a restrained text primary action, filled secondary/icon controls, and a Control Center icon that cannot be mistaken for the operating-system maximize command.
- Add a lightweight More menu that exposes the control center, contextual PowerPoint and phone-remote entry points, pinning, timer settings, and exit without permanently occupying the timer surface.
- Add an Expanded Control Center based primarily on `docs/design/presentation-timer-expanded-ui-v2.png`: a full-width Timer Hero, a lower PowerPoint/phone capability row, and a low-chrome duration strip within the same window and view-model lifetime.
- Give the Expanded Timer Hero the same state-appropriate primary action as Compact mode: Start in Ready, Pause in Running, Resume in Paused, and Reset only when applicable.
- Resize and restyle the same window when switching modes while preserving the authoritative Timer, PowerPoint, and Remote state supplied by the existing presentation-session service.
- Introduce warning and overtime presentation states, including a leading `+` overtime value, accessible text-plus-color status, localized labels, and keyboard/automation support.
- Replace the current free-form duration field in the primary workflow with 10, 15, 20, and 30 minute presets plus an accessible custom-duration dialog.
- Remove the permanently visible speaker-note preview and trusted-network explanation from the default timer surface; keep notes on the existing phone presenter and show remote diagnostics only where action is needed.
- Hide the pairing QR after a phone connects and expose the current QR through a contextual flyout; show only the authenticated device count rather than inventing device model or last-seen metadata that the current remote protocol does not provide.
- Keep a single always-on-top entry in the Expanded title bar and omit a placeholder Settings destination until the application has real settings to manage.
- Keep the PowerPoint COM adapter, monotonic timer, remote host, SignalR hub, pairing credentials, QR generation, and phone web UI behavior unchanged.
- Defer persistence of window position, duration, and pinning until there is a dedicated settings store; this UI-focused change retains those choices only for the current process lifetime.

## Capabilities

### New Capabilities

- `desktop-timer-experience`: Defines the Compact Timer, Expanded Control Center, progressive capability menu, shared state, timer visuals, duration selection, window behavior, localization, and accessibility contract.

### Modified Capabilities

None. The repository has no promoted main specs yet; the prior MVP behavior remains the foundation and this change adds a focused desktop-experience capability.

## Impact

- Primary implementation scope: `PresentationTimer.App` window, page, view model, theme resources, localized resources, and desktop UI verification.
- Test scope: pure presentation-state projection tests where practical, solution build/tests, UI smoke automation, and manual window/PowerPoint/phone checks.
- No public Core contract, Office COM, remote protocol, authentication, QR-token, or browser-client change is expected.
- No new runtime dependency or alternate desktop framework is introduced.

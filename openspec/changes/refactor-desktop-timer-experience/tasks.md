## 1. Baseline and presentation projection

- [x] 1.1 Record the current dirty-worktree files, run the focused App/Core/Remote tests and a Debug x64 solution build, and distinguish any pre-existing failure before UI edits.
- [x] 1.2 Add an internal pure timer-presentation projection for normal, warning, and `+hh:mm:ss` overtime text/state, clamped remaining ratio, and Ready/Running/Paused primary action without changing Core timer calculations.
- [x] 1.3 Add App tests for countdown formatting, Ready/Running/Paused primary actions, remaining-ratio boundaries, the exact 60-second warning boundary, zero, first overtime second, 1:32 overtime, multi-hour values, and preset/custom target classification.
- [x] 1.4 Extend `MainViewModel` with derived warning/action/menu/preset/progress properties and commands that continue to route Timer, PowerPoint, and Remote operations only through `IPresentationSessionService`.

## 2. Compact window shell

- [x] 2.1 Extend `WindowController` with explicit Compact, Expanded, always-on-top, and normal-close operations while preserving coordinated shutdown.
- [x] 2.2 Refactor `MainWindow.xaml` and `MainWindow.xaml.cs` to default to a 440×240 effective-pixel borderless Compact mode with a dedicated drag region, disabled resizing, optional small DWM corners, and DPI-aware sizing.
- [x] 2.3 Implement current-process compact-rectangle capture/restore, visible-work-area clamping, and Expanded presenter restoration with approximately 920×680 initial and 800×600 minimum effective-pixel bounds.
- [ ] 2.4 Add the single persistent Expanded title-bar pin, synchronize it with the Compact More toggle, and verify repeated presenter transitions keep the same window, drag behavior, caption actions, and always-on-top state.

## 3. Compact Timer experience

- [x] 3.1 Add Light, Dark, and High Contrast semantic resources for compact surface, normal timer, warning, overtime, success, dividers, and reusable presenter command styles without hardcoded usage-site colors.
- [x] 3.2 Replace the dashboard-first default visual tree with a dark low-glare Compact root containing the scalable authoritative timer, accessible state text, Expand, one state-appropriate Start/Pause/Resume action, Reset when applicable, and More.
- [x] 3.3 Implement the native More menu with contextual Control Center, phone remote, PowerPoint status, Always on Top, Timer Settings, and Exit behavior; do not add a settings page or manual PowerPoint lifecycle.
- [x] 3.4 Add and preserve complete Simplified Chinese and English resources for compact labels, tooltips, menu status, validation, and automation names.
- [ ] 3.5 Build and smoke-launch the Compact implementation; verify Start, Pause, Resume, Reset, warning, overtime, More, dragging, pinning, and normal exit before adding Expanded content.

## 4. Expanded Control Center

- [x] 4.1 Add an `x:Load`-switched Expanded root bound to the existing `MainViewModel`, following v2's full-width Timer Hero, adaptive PowerPoint/Remote row, and low-chrome duration strip while preserving all aggregate session state.
- [x] 4.2 Implement the Expanded Timer Hero with configured-target/status text, semantic remaining progress, Collapse, More, Start in Ready, Pause in Running, Resume in Paused, and Reset only outside Ready.
- [x] 4.3 Build the PowerPoint region with localized text-plus-indicator status, `current / total`, Previous/Next enablement, and a phone-note availability hint while omitting the full desktop note body.
- [x] 4.4 Build the phone region with stopped/start, starting, pairing QR, multiple-candidate selection, and connected-count states; after connection hide the persistent QR behind a Display Pairing QR flyout, omit unavailable device identity/last-seen data, and retain concise trusted-LAN, failure/retry, and End Session behavior.
- [x] 4.5 Build the duration-only strip with 10, 15, 20, and 30 minute presets plus a themed, localized custom-duration `ContentDialog`; omit duplicate pinning and placeholder Settings, retain invalid input, preserve the prior target, and disable changes outside Ready.
- [x] 4.6 Wire More-menu deep links and Expand/Collapse focus restoration so PowerPoint, phone, and timer-setting entries open the same Control Center at the requested section.
- [ ] 4.7 Verify Expanded Start transitions the shared Ready timer to Running and swaps to Pause in place, then verify Compact and Expanded share pause, resume, reset, warning, progress, and overtime state with no duplicate notifier or subscription.

## 5. Accessibility and visual polish

- [x] 5.1 Apply native typography styles, Fluent icons, 4-pixel-grid spacing, minimum 44×44 presenter actions, restrained capability surfaces/dividers, and v2's card-free Timer Hero hierarchy at supported minimum sizes.
- [x] 5.2 Add stable automation identifiers, localized accessible names, tooltips, logical tab order, keyboard accelerators, visible focus, and polite timer state announcements without announcing every refresh tick.
- [ ] 5.3 Verify Compact forced-dark treatment, Expanded system Light/Dark themes, and High Contrast system-brush behavior; fix clipped Chinese/English strings and multi-hour timer values at 100%, 150%, and 200% DPI.

## 6. Regression verification and documentation

- [x] 6.1 Update `scripts/ui-smoke.ps1` for the new Compact and Expanded automation identifiers and cover Expanded Ready-to-Running Start, Pause/Resume/Reset, semantic progress, More, Expand/Collapse, title-bar pinning, Display Pairing QR flyout, preset/custom duration validation, and basic window bounds.
- [x] 6.2 Run focused App tests, the full solution test suite, and Debug plus Release x64 builds; fix all new warnings and failures without modifying PowerPoint, Remote, or phone-Web behavior unnecessarily.
- [ ] 6.3 Smoke-launch through the repository WinUI workflow and verify repeated mode switching, drag, resize, close, and shutdown produce no crash, blank tree, stale focus, or orphan process.
- [ ] 6.4 Execute the available PowerPoint and phone manual checks for status, slide position, Previous/Next, QR candidate match, connected count, notes on phone, session end, and old-token rejection; record environment-dependent checks that cannot be run.
- [x] 6.5 Update desktop manual-verification documentation with the Compact/Expanded matrix, theme/DPI checks, known Windows 10 square-corner fallback, and confirmation that Timer, COM, SignalR, QR credentials, and phone UI contracts were preserved.

## 7. Presentation HUD and command polish

- [ ] 7.1 Remove the visible Compact status caption while retaining localized accessible timer state, replace the maximize-like glyph, and apply solid semantic primary/secondary/icon command styles with restrained sizes.
- [ ] 7.2 Add a third `PresentationHud` page/window mode at approximately 288×96 effective pixels with a dedicated drag region, current-process bounds, corner placement, and the same always-on-top state.
- [ ] 7.3 Enter HUD only when Start is invoked from Compact, keep Pause/Resume in HUD, return Reset to Compact, keep Expanded Start in place, and make active Control Center Collapse return to HUD without creating session state.
- [ ] 7.4 Update localized resources, smoke/manual verification coverage, run strict OpenSpec validation, focused/full tests, and Debug/Release x64 builds; leave environment-dependent launch/theme/DPI checks open when the WinUI launch prerequisite is unavailable.

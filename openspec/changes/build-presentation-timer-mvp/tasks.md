## 1. Solution foundation

- [x] 1.1 Create `PresentationTimer.sln` and the App, Core, PowerPoint, Remote, Core.Tests, and Remote.Tests projects at the paths in `design.md`; target .NET 10/Windows where appropriate and verify every project restores.
- [x] 1.2 Add only the specified dependency edges (App -> Core/PowerPoint/Remote, PowerPoint -> Core, Remote -> Core, tests -> subjects) and add an automated architecture check or project-reference assertion that prevents a Core -> infrastructure dependency.
- [x] 1.3 Enable nullable reference types, implicit usings, deterministic builds, and warnings appropriate for a greenfield solution without suppressing COM or async warnings globally.
- [x] 1.4 Pin WinUI/Windows App SDK, PowerPoint interop, ASP.NET Core framework, SignalR JavaScript, QR, and test dependencies in one centrally discoverable location; record licenses/provenance for bundled browser assets.
- [x] 1.5 Add a repository `.gitignore` and a minimal developer README with prerequisites and exact restore/build/test commands, without claiming that application features are implemented yet.
- [x] 1.6 Run a clean solution restore, build, and empty test pass; fix all scaffold warnings/errors so this commit point is buildable before adding behavior.

## 2. Core state and accurate timer

- [x] 2.1 Add immutable presentation, timer, remote-session, and aggregate session state records/enums matching the state model in `design.md`, including revision and stable error-code fields.
- [x] 2.2 Add result types and the narrow `IPresentationController`, `IRemoteSessionHost`, timer, and presentation-session service contracts without referencing UI, Office, ASP.NET, or QR types.
- [x] 2.3 Implement the production monotonic-clock adapter over `Stopwatch.GetTimestamp()` and a controllable fake clock in Core.Tests.
- [x] 2.4 Implement timer duration validation/configuration and Ready -> Running start behavior; add tests for valid 15-minute setup and rejected zero, negative, malformed, or unsupported values.
- [x] 2.5 Implement elapsed-time snapshot calculation from accumulated run segments; add a test proving a simulated 20-second notification/UI stall is included rather than lost.
- [x] 2.6 Implement Pause and Resume for countdown and overtime; add transition tests showing paused time does not accumulate.
- [x] 2.7 Implement Reset from Ready, Running, Paused, running overtime, and paused overtime; verify it restores the configured target and clears overtime.
- [x] 2.8 Add boundary tests for the zero crossing and long elapsed durations, asserting the timer remains running and overtime increases from actual elapsed time.
- [x] 2.9 Implement the thread-safe immutable session-state store that updates one component slice, increments revision, and publishes events only after releasing its lock.
- [x] 2.10 Implement the initial `PresentationSessionService` command/state coordination with fake infrastructure adapters; add tests proving presentation updates cannot overwrite newer timer/remote slices and stale presentation fields clear on disconnect.
- [x] 2.11 Run Core.Tests plus a clean solution build and keep this Core milestone buildable before introducing UI or Office dependencies.

## 3. Desktop timer vertical slice

- [x] 3.1 Create the WinUI application composition root and lifecycle container, constructing the Core timer/session service with placeholder presentation and remote adapters.
- [x] 3.2 Add a small main view model that projects immutable session state, marshals property notifications to the WinUI dispatcher, and exposes only application-service commands.
- [x] 3.3 Build duration entry and Start, Pause, Resume, and Reset controls with validation messages and correct enablement for every timer state.
- [x] 3.4 Build the dominant, distance-legible countdown/overtime display using platform typography/resources; verify 15:00, 00:00, and multi-hour overtime values do not clip at minimum size.
- [ ] 3.5 Add a periodic UI notifier that recomputes from the monotonic timer rather than decrementing state; manually block UI updates and verify the next display is accurate.
- [x] 3.6 Add compact PowerPoint and Remote status cards backed by the placeholder states so later infrastructure can be integrated without changing layout ownership.
- [ ] 3.7 Enforce a usable minimum/resizable window, add the always-on-top toggle through `AppWindow`, and verify system light, dark, and high-contrast resources remain readable.
- [x] 3.8 Run solution build/tests and a WinUI smoke launch exercising configure/start/pause/resume/reset/overtime; document the smoke result before continuing.

## 4. PowerPoint COM foundation

- [x] 4.1 Add the PowerPoint interop dependency only to `PresentationTimer.PowerPoint`, use early-bound embedded interop types, and prove Core/App view models have no Office references.
- [x] 4.2 Implement the isolated native `GetActiveObject`/ProgID registration wrapper with explicit HRESULT mapping for unavailable, not installed, and not running results.
- [x] 4.3 Implement the dedicated STA COM dispatcher with pre-start apartment selection, message-queue initialization, private work messages, `GetMessage`/`DispatchMessage`, asynchronous result completion, and orderly `WM_QUIT`.
- [x] 4.4 Add dispatcher tests that execute on STA, serialize concurrent work, propagate exceptions/cancellation, continue pumping queued work, and stop within a bounded timeout without PowerPoint.
- [x] 4.5 Implement a per-operation COM scope that tracks owned RCWs by reference identity and releases them once in reverse order; document which root/event objects are excluded.
- [x] 4.6 Implement PowerPoint installed/running attachment on the STA without creating or showing PowerPoint, and publish `Unavailable`, `NotRunning`, `NoPresentation`, and `NoSlideShow` states.
- [x] 4.7 Replace the desktop placeholder presentation adapter with the real monitor and show each readiness state while keeping timer controls usable.
- [ ] 4.8 Manually verify absent/not-running/no-presentation/no-slide-show states, then run build/tests before adding nested slide objects or commands.

## 5. PowerPoint state, notes, navigation, and recovery

- [x] 5.1 Implement one scoped snapshot read of the active slide-show window and map missing presentation/show collections to stable states without leaking RCWs.
- [ ] 5.2 Read `View.Slide.SlideIndex` and the active presentation `Slides.Count`; display the resulting `Slide X / Y` on desktop and verify against a real deck.
- [ ] 5.3 Read current-slide notes body/vertical-body placeholders as plain text, excluding slide artwork, header, footer, date, and slide-number placeholders.
- [x] 5.4 Extract line-ending/notes normalization into pure code and add tests for empty, multiline, multiple-body, and markup-like text.
- [x] 5.5 Implement `NextAsync` on the COM dispatcher, return not-ready/busy results safely, and refresh authoritative PowerPoint state after the native command.
- [x] 5.6 Implement `PreviousAsync` with the same command/result/state rules and no optimistic slide-number update.
- [x] 5.7 Add desktop Previous/Next controls for verification, wire them only through `PresentationSessionService`, and disable them outside Running slide-show state.
- [x] 5.8 Subscribe and unsubscribe `SlideShowBegin`, `SlideShowNextSlide`, `SlideShowEnd`, `AfterPresentationOpen`, `PresentationClose`, and active-window events on the COM STA.
- [ ] 5.9 Treat slide events as invalidations and post a deferred snapshot read; add a targeted manual check that `SlideShowNextSlide` never publishes the old or guessed next index.
- [ ] 5.10 Add the low-frequency reconciliation/reattach scheduler by posting to the STA queue; verify the app detects PowerPoint/slide show starting after app launch and catches an externally changed slide.
- [x] 5.11 Map call-rejected/busy and disconnected-server COM errors, add bounded message-pump-friendly retry for the former, and clear stale slide/notes state for the latter.
- [ ] 5.12 Implement PowerPoint exit/restart recovery and prove a new application RCW/event set is acquired rather than reusing the invalid instance.
- [x] 5.13 Implement adapter shutdown that stops reconciliation, unsubscribes events, releases owned RCWs, and never calls `Application.Quit`, closes a deck, kills a process, or blocks the WinUI thread.
- [ ] 5.14 Run build/tests and the PowerPoint manual checks through startup, detection, notes, Next/Previous, keyboard change, show end, PowerPoint exit/restart, and app exit leaving PowerPoint open.
- [x] 5.15 Add a desktop PowerPoint file picker for `.ppt`, `.pptx`, `.pptm`, `.pps`, and `.ppsx`, with cancellation leaving presentation state unchanged.
- [x] 5.16 Add `OpenPresentationAsync` through the Core service boundary and implement validated-path COM activation, read-only open/reuse, `SlideShowSettings.Run`, and authoritative refresh on the existing STA.
- [x] 5.17 Map unsupported/missing paths, unavailable COM registration, busy calls, file-open failures, and disconnections to stable user-safe results; never close the selected presentation or call `Application.Quit` during app shutdown.
- [x] 5.18 Add focused automated tests for validation, activation mapping, and service delegation; run the PowerPoint/Core/App test projects plus a solution build and OpenSpec validation.

## 6. Secured remote-session foundation

- [x] 6.1 Implement a session-scoped in-process remote-host factory with local static assets, a presentation-data-free loopback health endpoint, and bounded asynchronous start/stop; do not expose commands yet.
- [x] 6.2 Implement 32-byte cryptographic pairing-token generation, base64url encoding, hash-only validation with fixed-time comparison, and credential clearing; add lifecycle/uniqueness tests.
- [x] 6.3 Implement `/pair?t=...` validation that issues a separate random `HttpOnly; SameSite=Strict` per-browser session cookie and redirects to a token-free presenter URL.
- [x] 6.4 Implement the session-cookie authentication handler and require it for presenter entry, hub negotiation/connections, state reads, and commands; keep health free of protected data.
- [x] 6.5 Add HTTP integration tests proving missing, malformed, incorrect, ended-session, and prior-session credentials return invalid/expired results with no notes, slide, timer, or command data.
- [x] 6.6 Bundle a pinned official SignalR JavaScript client and license locally, add the minimal offline presenter HTML/CSS/JS shell, and verify it loads without Internet/CDN access after valid pairing.
- [x] 6.7 Enumerate labeled operational non-loopback IPv4 candidates and bind loopback plus eligible interface endpoints on non-privileged available ports; expose only successfully bound URLs.
- [x] 6.8 Generate the selected pairing QR locally and verify by decoding that its token-bearing URI exactly equals the visible desktop pairing URI.
- [x] 6.9 Wire Start/End Remote Session into the desktop card with Starting/Ready/Failed/Stopping states, URL selection, QR, retry, and immediate descriptor clearing on End.
- [x] 6.10 Add tests that End invalidates cookie/pairing credentials before host shutdown and a subsequent Start creates a new token and rejects the old QR.
- [x] 6.11 Run Remote.Tests and full solution build; verify no unauthenticated `/next`, `/previous`, state, notes, or generic control endpoint exists.

## 7. First authenticated phone-to-PowerPoint vertical slice

- [x] 7.1 Add an explicit `PresenterStateDto` projection that omits pairing tokens, token-bearing URLs, cookies, local-only diagnostics, and raw exception details; test its serialized shape.
- [x] 7.2 Implement the authenticated SignalR presenter hub with `GetState` and immediate full-state delivery after connection.
- [x] 7.3 Implement authenticated hub `Next` and `Previous` methods that call `PresentationSessionService` once and return structured not-ready/busy/success results.
- [x] 7.4 Implement a state broadcaster that sends revisioned full presenter snapshots after meaningful aggregate-state changes and never holds the Core state lock during network I/O.
- [x] 7.5 Complete the minimal phone UI for current/total slide, plain-text notes, connection label, and large Previous/Next touch targets; do not add timer polish yet.
- [x] 7.6 Add Remote integration tests with a fake presentation controller for authenticated connect, initial state, Next, Previous, broadcast after state change, and exactly-one command routing.
- [ ] 7.7 Run a real vertical-slice check: scan QR, open phone browser, tap Next, observe PowerPoint advance, then verify desktop and phone show the resulting slide and notes.
- [ ] 7.8 Record any COM/LAN findings from the vertical slice in the manual checklist and fix blockers before expanding remote behavior.

## 8. Complete real-time phone presenter

- [x] 8.1 Publish computed whole-second timer snapshots immediately on transitions and at the running cadence without using the notifier as the time source.
- [x] 8.2 Add Remaining/Overtime rendering to the phone UI, including prominent overtime styling and authoritative replacement after each heartbeat.
- [x] 8.3 Track authenticated hub connections and project zero/one-or-more phone connection status to the desktop without exposing connection identifiers.
- [ ] 8.4 Verify PowerPoint keyboard/clicker changes, show start/end, notes changes, timer transitions, and PowerPoint loss all broadcast full state without phone refresh.
- [x] 8.5 Add phone `connecting`, `connected`, `reconnecting`, `disconnected`, and `expired` states, disabling navigation whenever it cannot safely issue a command.
- [x] 8.6 Implement automatic reconnect with capped backoff; on reconnect invoke `GetState`, replace every presenter field, ignore older revisions, then re-enable controls.
- [x] 8.7 Ensure one tap submits one invocation, no navigation command is queued/retried across a disconnect, and an uncertain result waits for the authoritative snapshot.
- [x] 8.8 Render notes only through safe text APIs with preserved line breaks; add a browser/integration check that markup-like notes cannot execute.
- [x] 8.9 Add integration tests for disconnect/reconnect, missed changes, full resynchronization, revision ordering, timer resync, and session ending while reconnecting.
- [ ] 8.10 Manually verify responsive one-handed operation in portrait/landscape at representative iPhone and Android viewport sizes.

## 9. LAN, firewall, and network-change resilience

- [x] 9.1 Complete multiple-adapter URL selection with interface labels, distinct URLs, exact QR matching, and no claim that the first candidate is reachable.
- [x] 9.2 Handle no usable LAN address by retaining loopback health diagnostics while withholding a misleading phone QR/URL.
- [x] 9.3 Handle partial endpoint-bind failure and full listener failure, preserving healthy endpoints and unaffected timer/PowerPoint capabilities.
- [x] 9.4 Subscribe to debounced network-address changes, refresh endpoint candidates, and restart/rebind the host when required while preserving only the current live session credential store.
- [x] 9.5 Update/withdraw pairing URLs and QR after PC IP change and show that existing phones may need to reconnect or rescan rather than displaying stale connected state.
- [x] 9.6 Add remote diagnostics for same-LAN verification, adapter selection, VPN, Wi-Fi client isolation, local health, Windows Firewall, and managed-device policy.
- [x] 9.7 Audit the application and installer/publish configuration to prove they never elevate, create/delete firewall rules, disable protection, or answer the Windows consent prompt.
- [ ] 9.8 Add deterministic tests for address filtering/labeling and endpoint failure; manually test allowed, denied, and managed firewall paths where available.

## 10. Reliability, privacy, and shutdown hardening

- [x] 10.1 Add stable subsystem error codes and user-safe messages so raw COM, socket, hub, or authentication exception text is never sent to a phone.
- [x] 10.2 Configure bounded local structured logging that excludes notes, pairing tokens, cookies, and full token-bearing URIs; add a captured-log test for the pairing and invalid-token flows.
- [x] 10.3 Add same-origin/CORS restrictions, WebSocket Origin defense-in-depth, `Referrer-Policy: no-referrer`, and the trusted-LAN HTTP disclosure specified in `remote-session`.
- [x] 10.4 Implement the idempotent shutdown coordinator in the documented order: reject commands, revoke credentials, stop remote host, stop timer notifications, detach service events, then clean PowerPoint on STA.
- [x] 10.5 Add bounded shutdown tests for remote-host failure, repeated stop calls, connected phones, and STA cleanup exception; verify normal process exit without force-kill.
- [ ] 10.6 Repeat attach/detach, slide navigation, PowerPoint exit/restart, and app shutdown cycles while monitoring for orphan PowerPoint processes, duplicate events, stale notes, or monotonically growing COM references.
- [ ] 10.7 Exercise a running timer during UI blocking, PowerPoint exit, remote-host failure, and phone disconnect; verify elapsed-time accuracy and independent graceful degradation.
- [ ] 10.8 Finish desktop layout/status accessibility: text plus color, keyboard focus, AutomationProperties, large timer at minimum size, theme/high-contrast checks, and clear overtime/diagnostic states.
- [x] 10.9 Run all automated tests and a Release solution build with warnings reviewed; fix reliability/privacy regressions before packaging.

## 11. Verification, documentation, and dogfood packaging

- [x] 11.1 Add a fixture-driven PowerPoint manual checklist covering the cases in `design.md` and prepare a small numbered deck with empty, multiline, markup-like, hidden-slide, and final-slide notes cases.
- [ ] 11.2 Execute the full PowerPoint checklist with available 32-bit and 64-bit desktop PowerPoint installations and record environment/results without adding fake COM coverage infrastructure.
- [x] 11.3 Add an unpackaged self-contained x64 WinUI publish profile including .NET, Windows App SDK, ASP.NET Core, static phone assets, QR dependency, and Office interop metadata; keep NativeAOT disabled.
- [ ] 11.4 Publish from a clean checkout/output directory and verify the packaged files launch on a clean Windows user profile without separately installing .NET or Windows App SDK runtimes.
- [x] 11.5 Document install/portable launch, ordinary workflow, trusted-LAN security boundary, firewall troubleshooting, adapter selection, session revocation, and uninstall/rollback behavior in the README.
- [ ] 11.6 Run same-LAN phone checks on iPhone and Android where available, including QR open, Next/Previous, notes, timer/overtime, Wi-Fi interruption, reconnect, and expired old QR.
- [ ] 11.7 Run the Windows Firewall denied/allowed and PC IP-change scenarios against the published build and verify the app changes no system settings silently.
- [ ] 11.8 Execute the complete MVP end-to-end scenario from `desktop-presenter-workspace/spec.md`, including direct PowerPoint slide changes, overtime, End Remote Session, and immediate old-token failure.
- [x] 11.9 Run final `dotnet build`, `dotnet test`, Release publish, OpenSpec validation, and artifact smoke checks; record exact commands/results and stop without adding non-goal features.

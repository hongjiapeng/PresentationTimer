# Manual verification

Record the date, Windows version, build commit/archive identifier, PowerPoint version and bitness, network, and device/browser for every run. Do not mark an unavailable environment as passed.

## Compact and Expanded desktop experience

| Area | Check | Expected result | Result / evidence |
|---|---|---|---|
| Compact launch | Start at 100%, 150%, and 200% display scale | Borderless 440×240 effective-pixel timer; no PPT, phone, QR, notes, or duration form | Not run |
| Compact controls | Ready → Start; Running → Pause; Paused → Resume; Reset | Exactly one primary action; Reset only outside Ready; controls remain clickable beside the drag region | Not run |
| Timer states | Cross 01:00, 00:00, +00:00:01, and multiple hours | Warning includes 01:00 through zero; overtime uses `+hh:mm:ss`; state text remains available without color | Not run |
| More menu | Open every state-dependent entry and toggle Always on Top | Entries reflect current PPT/phone state; Timer Settings deep-links to duration; Exit performs coordinated shutdown | Not run |
| Mode switching | Drag Compact, expand, resize, collapse, repeat 20 times | Same window and session; prior Compact rectangle returns on a visible work area; pin state stays synchronized | Not run |
| Expanded hierarchy | Inspect at 920×680 and minimum 800×600 effective pixels | Full-width card-free Timer Hero, lower PPT/Remote modules, and duration-only strip follow the v2 reference | Not run |
| Expanded timer | Start, Pause, Resume, Reset and inspect progress | Action swaps in place; remaining progress is determinate, fixed while paused, and empty in overtime | Not run |
| Duration | Select 10/15/20/30, accept `1:05:30`, reject malformed input | Authoritative target updates only while Ready; invalid input keeps the prior target and dialog open | Not run |
| Remote QR | Pair zero, one, and multiple authenticated phones | QR is inline before connection; after connection only count remains and Display Pairing QR opens the same material | Not run |
| Themes | Expanded Light/Dark; Windows High Contrast; Compact in both app themes | Compact stays low-glare; High Contrast uses system colors; focus and text remain distinguishable | Not run |
| Localization | Run `en-US` and `zh-CN` at all supported scales | Labels, menus, tooltips, validation, automation names, and multi-hour values do not clip | Not run |
| Windows fallback | Repeat Compact check on supported Windows 10 host | Square corners are acceptable; drag, input, sizing, and shutdown remain functional | Not run |

The desktop refactor must not change the monotonic Timer calculation, PowerPoint COM lifecycle, SignalR hub and remote host behavior, QR credentials/token rotation, or phone web UI contract. Validate those preserved boundaries with the automated Core/Remote suites and the integration matrices below.

### Implementation verification record — 2026-08-29

- Passed App/Core/Remote automated suites: 23 + 50 + 23 tests.
- Passed Debug and Release x64 solution builds with zero warnings and zero errors.
- Passed strict OpenSpec validation and PowerShell syntax validation for `scripts/ui-smoke.ps1`.
- The repository WinUI workflow built successfully, but this host did not have the `winapp` CLI in `PATH`; the workflow therefore skipped launch. Compact/Expanded runtime, theme/DPI, PowerPoint, phone, and DWM checks below remain explicitly **Not run**, rather than being reported as passes.

## PowerPoint fixture

Use `tests/fixtures/PresentationTimer.PowerPointFixture.pptx`, whose six numbered slides cover baseline, empty notes, multiline notes, literal markup-like text, a genuinely hidden slide, and a final slide. Keep the deck open after every app-shutdown check. Record detailed findings in [powerpoint-manual-checklist.md](powerpoint-manual-checklist.md).

| # | Scenario | Expected result | Result / evidence |
|---|---|---|---|
| 1 | PowerPoint absent/not registered | App remains usable and reports unavailable | Not run |
| 2 | Installed but not running | Reports not running; timer remains usable | Not run |
| 3 | Running with no presentation | Reports no presentation | Not run |
| 4 | Deck open without slide show | Reports that slide show must start | Not run |
| 5 | App starts first, slide show later | Detects the show automatically | Not run |
| 6 | Current/total and all fixture notes | Exact index/count; safe normalized plain text | Not run |
| 7 | Desktop and phone Previous/Next | One action advances one native slide | Not run |
| 8 | Keyboard/clicker changes slide | Desktop and phone update without refresh | Not run |
| 9 | Start/end/restart slide show | State clears and reconnects without stale data | Not run |
| 10 | PowerPoint busy/modal | Safe busy state; timer and app remain responsive | Not run |
| 11 | PowerPoint exits then restarts | No crash; stale notes clear; new instance attaches | Not run |
| 12 | App exits during active deck/show | PowerPoint and deck remain open | Not run |
| 13 | Repeat attach/detach/navigation 20 times | No duplicate events, orphan process, or steady RCW growth | Not run |

Run this matrix with available 32-bit and 64-bit desktop PowerPoint installations. The Office bitness is evidence to record, not a build-target requirement because PowerPoint is an out-of-process COM server.

## Phone, LAN, and release matrix

- iPhone Safari and Android Chrome: scan, initial state, Previous/Next, notes, countdown, overtime, portrait, and landscape.
- Disable/re-enable Wi-Fi: clear disconnected state, automatic reconnect, full-state resync, and no duplicated navigation.
- End session during reconnect, then confirm the old QR and old browser cookie fail immediately.
- Windows Firewall denied and allowed on Private network; confirm the app changes no system settings.
- Change the PC IP/network, wait for endpoint rebinding, select the replacement adapter URL/QR, and confirm stale pairing material is withdrawn.
- Launch the Release portable output from a clean Windows user profile without a separately installed .NET or Windows App SDK runtime.

## MVP end-to-end

Start the app, deck, and slide show; observe Slide 1 / N; start a 15-minute timer; start/scan remote; verify notes and timer; advance from the phone; advance directly in PowerPoint; cross 00:00 into overtime; end the session; then verify the old QR is unauthorized.

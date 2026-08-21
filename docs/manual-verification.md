# Manual verification

Record the date, Windows version, build commit/archive identifier, PowerPoint version and bitness, network, and device/browser for every run. Do not mark an unavailable environment as passed.

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

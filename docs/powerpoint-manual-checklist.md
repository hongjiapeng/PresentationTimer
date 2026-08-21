# PowerPoint manual verification checklist

Use `tests/fixtures/PresentationTimer.PowerPointFixture.pptx` for repeatable PowerPoint integration checks. Record the Office bitness, Windows build, PowerPoint version, application build, phone/browser versions, and tester/date before executing the matrix.

## Fixture map

| Slide | Case | Expected speaker notes |
| --- | --- | --- |
| 01 | Baseline | `Baseline notes — slide 1` |
| 02 | Empty notes | Empty; prior notes must be cleared |
| 03 | Multiline notes | Three lines: `Line one`, `Line two`, `Line three` |
| 04 | Markup-like notes | Literal `<script>` and `<strong>` text; never interpreted as HTML |
| 05 | Hidden slide | `Hidden-slide notes fixture`; slide is marked hidden in the deck |
| 06 | Final slide | `Final-slide notes fixture` |

## Environment record

- Tester/date: Pending
- Windows build: Pending
- Office edition/version/bitness: Pending
- PresentationTimer build or commit: Pending
- iPhone/browser: Pending
- Android/browser: Pending
- Network/adapters/firewall policy: Pending

## Thirteen-case matrix

All rows start as **Pending**. Change a row to Pass, Fail, Blocked, or Not available only after executing it, and add concise evidence.

| # | Scenario and expected result | Status | Evidence / finding |
| --- | --- | --- | --- |
| 1 | PowerPoint absent or COM class not registered: app reports unavailable; timer remains usable. | Pending | |
| 2 | PowerPoint installed but not running: app reports not running and does not launch PowerPoint. | Pending | |
| 3 | PowerPoint running with no presentation: app reports no presentation and clears slide/notes. | Pending | |
| 4 | Fixture open without slide show: app reports no slide show; navigation remains disabled. | Pending | |
| 5 | Start app first, then open fixture and start slide show: reconciliation attaches without restarting the app. | Pending | |
| 6 | Traverse fixture: current/total index is authoritative; empty, multiline, markup-like, hidden, and final-slide behavior matches the fixture map. | Pending | |
| 7 | Desktop and paired-phone Previous/Next each submit once and both views converge on the resulting PowerPoint state. | Pending | |
| 8 | Keyboard and physical clicker navigation synchronize desktop and phone without refresh; no guessed old/new index is published. | Pending | |
| 9 | Start, end, and restart the slide show: stale slide/notes are cleared and a new running state is acquired. | Pending | |
| 10 | While PowerPoint is busy during animation or modal UI: bounded retry occurs, UI stays responsive, and failure is recoverable. | Pending | |
| 11 | Exit PowerPoint during a read, restart it, reopen the fixture, and start the show: app obtains a new COM application/event set. | Pending | |
| 12 | Exit PresentationTimer while fixture and PowerPoint remain open: app exits normally and never calls `Application.Quit`. | Pending | |
| 13 | Repeat attach/detach and app shutdown cycles while monitoring process/reference behavior: no orphan PowerPoint process or steady COM-reference growth. | Pending | |

## LAN, phone, theme, and packaging addendum

Record these separately because they require physical devices, OS UI, policy, or a clean profile:

- iPhone and Android portrait/landscape one-handed use, including reconnect and expired-session states.
- Multiple adapters, VPN/client-isolation diagnostics, PC IP change and QR reselection/rescan.
- Windows Firewall allowed, denied, and managed-policy paths; the app must not change rules or elevate.
- System light, dark, and high-contrast themes at the minimum window size, including always-on-top.
- Published self-contained x64 output on a clean Windows user profile with supported 32-bit and 64-bit desktop PowerPoint where available.

## Result summary

- Overall status: Pending
- Blocking defects: None recorded yet
- Follow-up issue/change IDs: None recorded yet

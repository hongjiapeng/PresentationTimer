## Context

See `proposal.md` for motivation and `specs/desktop-timer-experience/spec.md` for observable behavior.

The desktop application currently has one `MainWindow`, one `MainPage`, and one process-lifetime `MainViewModel`. The view model subscribes to `IPresentationSessionService`, which already merges immutable Timer, PowerPoint, and Remote slices into one revisioned state. It also owns the existing timer commands, slide navigation, remote-session lifecycle, pairing candidates, and QR bitmap projection. `WindowController` is the deliberately small seam between page and top-level window behavior.

The current `MainPage` is a scrolling dashboard with a large timer card, free-form duration input, PowerPoint/notes card, remote/QR card, and always-open trusted-network warning. `MainWindow` always opens at 1120×760 effective pixels and uses the WinUI `TitleBar` control with Mica. The PowerPoint controller already attaches automatically and reconciles every two seconds, while the remote host already revokes pairing and browser credentials at End Session. Those lifecycle semantics must not be duplicated in presentation code.

The reference images define hierarchy and states rather than literal pixels. `presentation-timer-interaction-v1.png` remains the Compact-state reference. `presentation-timer-expanded-ui-v2.png` supersedes v1 as the primary Expanded reference because its full-width Timer Hero, lower capability row, and bottom duration strip preserve a much stronger timer-first hierarchy. The implementation will retain that hierarchy while omitting unsupported device metadata, duplicate pin/settings controls, and purely decorative progress behavior.

## Goals / Non-Goals

**Goals:**

- Keep one view-model and application-service subscription alive while switching between Compact, Presentation HUD, and Expanded presentation trees.
- Make top-level window mode changes reliable across DPI, display-topology, resize, drag, pinning, and shutdown.
- Keep presentation-state formatting deterministic and testable without introducing a second timer or infrastructure state cache.
- Use native WinUI controls and theme resources for flyouts, dialogs, title bar, selection, focus, localization, and High Contrast.
- Preserve the existing security disclosure by moving it into the active remote area rather than showing it permanently.

**Non-Goals:**

- Changing Timer, PowerPoint COM, SignalR, remote hosting, speaker-note extraction, pairing, or browser-client protocols.
- Adding a settings/navigation framework, a second page/window, real full-screen mode, or a second timer view model.
- Persisting window position, pinning, or duration across process restarts.
- Adding a manual PowerPoint connection command when the controller already auto-attaches and reconciles.
- Pixel-matching the dark design images at the cost of system theme, localization, or accessibility behavior.

## Decisions

### 1. Keep one page and one view model; switch only presentation mode

`MainPage` will host three mutually exclusive top-level roots: `CompactRoot`, `PresentationHudRoot`, and `ControlCenterRoot`. `x:Load` will bind them to a small shell-mode enum projection so hidden trees are unloaded, while all roots bind to the same existing `MainViewModel` instance. Entering a mode changes page presentation state and asks `WindowController` to apply the corresponding top-level window behavior; it never reconstructs the page, view model, service, or timer notifier.

Starting from Compact remains Compact and swaps Start to Pause in place. Presentation HUD is entered only through an explicit More-menu action while Running or Paused; pausing and resuming then retain HUD, and Reset returns it to Compact. Starting from Expanded deliberately remains Expanded so configuration and connectivity work is not unexpectedly hidden. Collapsing Expanded selects HUD when the timer is Running or Paused and Compact when it is Ready.

Window-mode state is a shell concern, so `MainPage` will own it along with current-process pinning and focused Control Center section. `MainViewModel` remains responsible for projecting aggregate session state and executing application-service commands. Thin XAML-root operations such as showing a `ContentDialog` and moving focus after expansion remain in page code-behind; business validation and timer configuration remain in the view model.

Alternative considered: separate Compact and Control Center pages. Rejected because navigation would complicate view-model lifetime, QR bitmap ownership, focus restoration, and the guarantee that both modes observe the same subscription.

Alternative considered: embed window mode in Core session state. Rejected because window shape is neither domain state nor browser-visible state.

### 2. Centralize derived timer presentation without changing Timer domain logic

Add an internal, pure presentation projection that accepts `TimerSnapshot` and returns formatted text plus Normal, Warning, or Overtime visual state. It will:

- retain countdown formatting as `mm:ss` below one hour and `h:mm:ss` at or above one hour;
- use Warning from 60 seconds through zero;
- format overtime with a leading `+` and at least `hh:mm:ss`, including `+00:00:01` for the first overtime second;
- calculate a clamped remaining ratio as `Remaining / Target`, from 1 in Ready to 0 at and after overtime;
- select Start in Ready, Pause in Running, and Resume in Paused as the single primary timer action, with Reset available only outside Ready;
- expose localized accessible status separately from color.

`MainViewModel.ApplyState` will update `TimerDisplay`, `IsWarning`, `IsOvertime`, primary action visibility/capability, contextual menu text, selected duration preset, and the existing subsystem projections from the same incoming aggregate snapshot. No UI-side decrement, elapsed accumulator, or second timer is added.

Alternative considered: bind three duplicate timer `TextBlock` elements through `x:Load`. Rejected because it duplicates automation peers and makes focus/live-region behavior harder to reason about.

### 3. Use progressive disclosure with native flyout and dialog controls

The Compact More button will own a `MenuFlyout` with localized `MenuFlyoutItem` and `ToggleMenuFlyoutItem` entries. Items bind to current view-model status text and existing commands. Entries that need to select a Control Center area will call one shell expansion method with a section target, then move keyboard focus to that region after layout.

The active Remote region will use a native `Flyout` for Display Pairing QR. Before the first authenticated connection the QR may remain inline to make pairing obvious; after a phone connects the card collapses to the real authenticated connection count and the flyout becomes the QR entry point. The flyout displays the existing selected QR/URL and never generates a replacement token merely because it was opened. Candidate selection stays with the QR content when multiple endpoints exist.

The duration row will use quick preset buttons with a single selected state and a native `ContentDialog` for Custom. The dialog is assigned the page `XamlRoot`, uses the current theme, keeps invalid input open, and delegates parsing/configuration to the existing duration path. Presets and Custom are disabled outside Ready. A custom duration that is not one of the four presets maps back to the Custom selected state.

No standalone Settings page will be created. The More menu's Timer Settings entry expands and focuses the duration region. The PowerPoint entry expands and focuses its status region; it does not pretend to connect manually because monitoring is automatic.

### 4. Give Compact, Presentation HUD, and Expanded intentionally different window chrome

`MainWindow` remains an `OverlappedPresenter` window in both modes so always-on-top and resizing remain available.

Compact mode will:

- hide the existing `TitleBar` control;
- call `OverlappedPresenter.SetBorderAndTitleBar(false, false)`;
- use the entire non-interactive timer display region through `Window.SetTitleBar`, excluding command buttons, so the obvious central surface drags the window;
- resize to 440×240 effective pixels using `GetDpiForWindow` conversion;
- request small rounded DWM corners when supported and tolerate an unsupported result;
- request no DWM border color in Compact and HUD, restoring the system default border in Expanded;
- prevent user resizing by setting presenter resizability off while compact.

Presentation HUD will reuse Compact's borderless presenter and small-corner request, register its own non-interactive time region as the drag surface, disable resizing/caption actions, and resize to 288×96 effective pixels. Its first transition preserves the current window position while changing only the size; later user dragging is retained for the process lifetime and clamped after display or DPI changes.

Expanded mode will:

- restore border/resizability and the existing WinUI `TitleBar` with system caption actions;
- place the sole persistent always-on-top toggle in the title bar and synchronize it with the Compact More toggle;
- register that title bar as the drag region;
- resize an uncustomized first expansion to approximately 920×680 effective pixels;
- enforce an approximately 800×600 effective-pixel minimum while allowing later user resize.

Before changing modes, `MainWindow` records the active `RectInt32`. On restoration it converts the retained size for current DPI, clamps the rectangle to a current `DisplayArea.WorkArea`, moves, and resizes the same `AppWindow`. This is session-only state. `WindowController` gains explicit EnterCompact, EnterPresentationHud, EnterExpanded, SetAlwaysOnTop, and RequestClose operations; normal close still flows through the current coordinated shutdown handler.

Alternative considered: use `CompactOverlayPresenter`. Rejected because it imposes picture-in-picture semantics and sizing constraints that do not match the timer or Expanded transition.

Alternative considered: implement drag entirely with `WM_NCLBUTTONDOWN`. The dedicated WinUI title-bar region is preferred; a small Win32 fallback is permitted only if runtime smoke validation finds a borderless drag regression on the supported SDK.

### 5. Implement the v2 Timer Hero hierarchy without unsupported chrome

Compact uses one low-glare dark surface, a centered `Viewbox` containing a `DisplayTextBlockStyle` timer, and one bottom command row. It removes the visible Ready/Remaining-Time caption and keeps that information in the timer's accessible name and polite state announcement. Its Start/Pause/Resume control is a centered presenter-sized text button rather than a full-width bar. Reset, More, and Control Center use filled semantic surfaces without opacity-only borders; the Compact expand action uses the system `FullScreen` outward-arrow glyph and Expanded uses the paired `BackToWindow` inward-arrow glyph, keeping the mode transition easy to remember while remaining distinct from the caption maximize command.

Presentation HUD uses the same solid low-glare surface, a draggable time region, and three 40×40 actions at most: Pause or Resume, Control Center, and More. Reset is moved into More so the persistent surface stays narrow. The HUD uses the same timer foreground semantics and accessible status as the larger roots.

Expanded follows the v2 composition in three vertical bands:

1. a full-width Timer Hero containing the authoritative value, configured-target/status subtitle, determinate remaining-ratio indicator, and Collapse / Start-Pause-Resume / Reset / More command row;
2. a lower adaptive capability row with PowerPoint and Remote modules of similar but content-driven width;
3. a full-width, low-chrome duration strip containing 10, 15, 20, 30, and Custom choices only.

The Timer Hero has no surrounding card so it owns the visual field. The capability modules use restrained layered surfaces and the duration strip uses a subtle separator rather than equal-elevation dashboard cards. The v2 curved progress mark is treated as functional remaining progress, not decoration: it starts full, decreases toward zero, stops while paused, changes to Warning treatment at the threshold, and is empty during overtime. Prefer a native determinate progress control with restrained styling; reproduce the exact curve only if it remains theme-aware, accessible, and does not add a fragile custom renderer.

The Timer Hero renders exactly one primary state action in a stable location. Ready shows Start and transitions the existing timer to Running; Running shows Pause; Paused shows Resume. Reset is absent in Ready and appears in other states. The action changes in place so the user does not have to relearn the layout after starting.

The PowerPoint region shows state, slide `current / total`, navigation, and a concise note-availability hint. It does not render the full note body. The Remote region shows only Start while stopped; while waiting for pairing it exposes QR/URL and candidate selection; after a phone connects it shows only the authenticated count, Display Pairing QR, End Session, and the concise trusted-LAN disclosure. It never renders the v2 mock's phone model or last-connected time because those values are absent from the remote contract. Network diagnostics appear only on failure.

The title-bar pin is the Expanded mode's only persistent always-on-top entry. The duplicate bottom Toggle and placeholder Settings item from the visual mock are omitted; Timer Settings remains a Compact More deep link to the duration strip rather than a separate destination.

Theme dictionaries define semantic Normal, Warning, Overtime, Success, and surface brushes for Light, Dark, and High Contrast. Usage sites use `ThemeResource`; High Contrast uses only system color brushes and state labels remain visible.

### 6. Keep localized resources and automation as part of the UI contract

All visible and accessible strings—including More entries, icon tooltips, timer state labels, preset labels, custom dialog text, Collapse, subsystem hints, and concise remote disclosure—will be added to both `zh-CN` and `en-US` `.resw` files. Existing resource changes in the working tree are user-owned and will be preserved.

The visual tree will use logical tab order, native semantic controls, `AutomationProperties.Name` for icon-only controls, and stable `AutomationProperties.AutomationId` for smoke automation. The timer live region will be Polite and announce state transitions rather than every 200 ms refresh. Focus returns to the Expand control after collapse and moves to the requested section after a More-menu deep link.

### 7. Verify presentation behavior at the narrowest stable seams

Add MSTest coverage for the pure timer projection: Ready/Running/Paused formatting and primary action, remaining-ratio calculation, exact warning boundary, zero, first overtime second, multi-hour overtime, and duration-preset selection. Existing Core and Remote tests remain the regression suite for business behavior.

Update `scripts/ui-smoke.ps1` to target the new stable automation identifiers and cover Compact plus Expanded Start/Pause/Resume/Reset, Expand/Collapse, More, title-bar pinning, Display Pairing QR flyout, duration preset/custom validation, and basic window bounds. Manual verification covers borderless drag/rounding, DPI/display changes, actual PowerPoint status/navigation, QR scan, connected count, session end/token failure, and theme/High Contrast because those depend on OS or external applications.

## Risks / Trade-offs

- [Toggling presenter chrome can produce stale sizing or a transient frame on some Windows builds] → Centralize the order of presenter, title-bar, move, and resize operations in `MainWindow`; smoke both directions repeatedly before polishing.
- [A fixed compact size can clip long localized or multi-hour values] → Scale only the timer through a bounded `Viewbox`, reserve command space, test English/Chinese and multi-hour overtime at 100–200% DPI.
- [Unloading one root with `x:Load` can drop focus and reopen flyouts incorrectly] → Close transient UI before switching, restore focus explicitly after layout, and keep session/view-model objects outside the unloaded root.
- [Automatically shrinking on Start makes the primary action feel discontinuous] → Start stays in its current Compact or Expanded surface; HUD requires an explicit menu action, while Collapse chooses HUD only for Running or Paused.
- [A HUD can still cover meaningful slide content] → Keep it substantially smaller than Compact, preserve the user's current position on first entry, and retain later user-selected HUD positions for the process lifetime.
- [Forced dark Compact styling can conflict with accessibility themes] → Apply dark theme only to the compact child surface and supply a High Contrast dictionary using system brushes; do not hardcode foreground colors at usage sites.
- [Current PowerPoint auto-monitoring has no explicit refresh command] → Represent the real status and focus the PowerPoint region; retain the existing two-second reconciliation instead of adding a misleading connection button.
- [UI-only tests cannot prove COM, LAN, or DWM integration] → Keep automated tests at deterministic seams and require the existing/manual integration matrix before completion.
- [Adding all requested menu entries could recreate settings complexity] → Timer Settings is a deep link to the existing Control Center duration region; no navigation framework or empty settings page is added.
- [The v2 curved progress mark could become expensive decorative chrome] → Implement remaining-ratio semantics first with a native determinate control and adopt the curve only if it survives theme, High Contrast, localization, and DPI verification.
- [The v2 connected-device mock shows data the server does not expose] → Render only authenticated connection count and keep device identity/last-seen metadata out of scope rather than changing the remote protocol for visual fidelity.

## Migration Plan

1. Add and test timer presentation projection plus derived view-model properties without changing the existing XAML layout.
2. Add window-mode APIs and implement Compact root as the default; build and smoke timer state/actions, drag, pinning, and More.
3. Replace the legacy dashboard root with the Expanded Control Center; wire expansion, collapse, duration presets, PowerPoint, and Remote to existing commands/state.
4. Apply semantic theme resources, localization, keyboard/focus behavior, and automation identifiers; update smoke/manual checks.
5. Run the focused App tests, full solution tests, Debug/Release builds, smoke launch, and available PowerPoint/phone manual checks.

Rollback is a source revert of the App-layer, test, script, and documentation changes. No Core schema, persisted data, credential format, or remote protocol migration is involved.

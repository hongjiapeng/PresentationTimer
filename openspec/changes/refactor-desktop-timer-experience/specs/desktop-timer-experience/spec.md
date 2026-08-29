## Purpose

Defines a focused desktop presentation-timer experience in which the live time is dominant, advanced presenter capabilities are progressively disclosed, and every desktop mode reflects the same authoritative session state.

## ADDED Requirements

### Requirement: Compact Timer is the default desktop mode
The application SHALL open in a small Compact Timer mode whose dominant element is the current timer value. The compact surface SHALL omit persistent PowerPoint status, slide position, speaker notes, phone connection details, QR codes, network instructions, and duration-entry forms.

#### Scenario: Application starts in compact mode
- **WHEN** the application window is first activated
- **THEN** it presents the configured timer value in Compact Timer mode
- **AND** only the timer value, essential timer actions, Expand, and More are persistently visible

#### Scenario: A subsystem is unavailable during a talk
- **WHEN** PowerPoint disconnects or the phone remote is stopped while Compact Timer mode is visible
- **THEN** the timer remains visible and usable without a persistent diagnostic panel displacing it

### Requirement: Compact Timer exposes state-appropriate controls
Compact Timer mode SHALL provide Expand, one state-appropriate primary timer action, Reset when applicable, and More as keyboard-focusable controls. Start SHALL be available in Ready, Pause in Running, and Resume in Paused; unavailable actions SHALL not appear as competing primary actions.

#### Scenario: Ready timer controls
- **WHEN** the timer is Ready
- **THEN** Compact Timer offers Start as its primary timer action
- **AND** activating Start begins the existing authoritative timer

#### Scenario: Running timer controls
- **WHEN** the timer is Running
- **THEN** Compact Timer offers Pause and Reset
- **AND** activating Pause changes the shared timer state to Paused

#### Scenario: Paused timer controls
- **WHEN** the timer is Paused
- **THEN** Compact Timer offers Resume and Reset
- **AND** activating Resume continues the same timer from its paused elapsed time

#### Scenario: Reset from a non-ready state
- **WHEN** Reset is activated while the timer is Running, Paused, or in overtime
- **THEN** the timer returns to Ready at the currently configured target duration

### Requirement: Timer visuals communicate normal, warning, and overtime states
The timer display SHALL derive its value from the authoritative timer snapshot rather than a view-owned countdown. A countdown above one minute SHALL use the normal timer treatment, remaining time from `01:00` through `00:00` SHALL use a warning treatment, and negative remaining time SHALL use a critical overtime treatment with a leading `+`. Expanded mode SHALL show the configured target and a determinate remaining-ratio indicator that is full in Ready, decreases toward zero while Running, remains fixed while Paused, and is empty in overtime. State and progress meaning SHALL be conveyed through accessible text in addition to color or shape.

#### Scenario: Warning threshold is reached
- **WHEN** a running timer reaches exactly 60 seconds remaining
- **THEN** the displayed value is `01:00`
- **AND** the timer enters the warning treatment without changing its run state

#### Scenario: Timer crosses into overtime
- **WHEN** elapsed time exceeds the configured target by 1 minute and 32 seconds
- **THEN** the desktop displays `+00:01:32`
- **AND** exposes an accessible overtime status independent of the critical color

#### Scenario: Timer notification is delayed
- **WHEN** UI rendering is delayed while the timer is running
- **THEN** the next rendered value reflects actual elapsed monotonic time rather than the number of missed UI updates

#### Scenario: Remaining progress follows authoritative time
- **WHEN** half of the configured target has elapsed
- **THEN** the Expanded remaining-ratio indicator represents 50 percent remaining
- **AND** pausing leaves that indicator fixed until the timer is resumed or reset

#### Scenario: Remaining progress reaches overtime
- **WHEN** the authoritative remaining time becomes negative
- **THEN** the remaining-ratio indicator is empty
- **AND** the critical overtime value remains the primary indication of continuing elapsed overtime

### Requirement: More menu progressively exposes presenter capabilities
The More control SHALL open a lightweight menu containing Open Control Center, contextual phone remote, contextual PowerPoint status, Always on Top, Timer Settings, and Exit actions. The menu SHALL not add a separate settings page or a second presentation connection lifecycle.

#### Scenario: Remote session is stopped
- **WHEN** More is opened while no remote session is active
- **THEN** the phone item communicates that the remote can be started
- **AND** activating it starts the existing remote-session workflow

#### Scenario: Remote session is active
- **WHEN** More is opened during an active remote session
- **THEN** the phone item includes the authenticated device count
- **AND** activating it opens the Control Center with the phone section available

#### Scenario: PowerPoint status changes
- **WHEN** More is opened after the monitored PowerPoint state changes
- **THEN** the PowerPoint item communicates the current connection state
- **AND** activating it opens the Control Center without starting or closing PowerPoint

#### Scenario: Pinning is toggled from the menu
- **WHEN** the Always on Top menu item is toggled
- **THEN** the current window updates its topmost state
- **AND** the checked state remains synchronized with the Expanded title-bar pin for the current application session

#### Scenario: Exit is selected
- **WHEN** the user selects Exit from More
- **THEN** the application performs the existing coordinated shutdown and closes normally

### Requirement: Expanded Control Center manages the same session
Activating Expand or Open Control Center SHALL transform the existing window into an Expanded Control Center. Following the v2 reference hierarchy, the expanded surface SHALL contain a full-width Timer Hero, a lower PowerPoint and phone-remote capability row, and a low-chrome target-duration strip, all driven by the same session and command gateway used by Compact Timer. The Timer Hero SHALL expose one state-appropriate primary action: Start in Ready, Pause in Running, and Resume in Paused; Reset SHALL appear only when applicable.

#### Scenario: Compact timer expands
- **WHEN** the user activates Expand while the displayed timer is `08:42`
- **THEN** the Control Center timer initially displays `08:42` from the same authoritative timer snapshot
- **AND** no second timer is created or started

#### Scenario: Expanded timer changes state
- **WHEN** the user pauses, resumes, or resets from the Control Center
- **THEN** collapsing back to Compact Timer immediately reflects that same state and value

#### Scenario: Ready timer starts from Expanded mode
- **WHEN** the authoritative timer is Ready and the Control Center is visible
- **THEN** the Timer Hero presents Start as its primary action
- **AND** activating Start changes the shared timer state to Running
- **AND** the primary action changes to Pause without navigating or creating another timer

#### Scenario: Paused timer resumes from Expanded mode
- **WHEN** the authoritative timer is Paused and the Control Center is visible
- **THEN** the Timer Hero presents Resume as its primary action
- **AND** activating Resume continues the same authoritative timer

#### Scenario: Control Center collapses
- **WHEN** the user activates Collapse from the Control Center
- **THEN** the existing window returns to Compact Timer mode without ending the timer, PowerPoint monitoring, or remote session

#### Scenario: Expanded pinning has one persistent entry
- **WHEN** the Control Center is visible
- **THEN** its title bar contains the single persistent Always on Top control
- **AND** the duration strip does not duplicate pinning or expose an empty Settings destination

### Requirement: Control Center exposes PowerPoint control without blocking the timer
The PowerPoint region SHALL show a text-plus-indicator connection state. While a slide show is running it SHALL show current and total slide numbers and offer Previous and Next; otherwise it SHALL show an actionable status without preventing any timer action.

#### Scenario: Slide show is connected
- **WHEN** the authoritative PowerPoint state reports slide 7 of 18 in a running slide show
- **THEN** the PowerPoint region displays `7 / 18`
- **AND** enables Previous and Next through the existing presentation command path

#### Scenario: PowerPoint is not ready
- **WHEN** PowerPoint is not running, has no presentation, has no slide show, is unavailable, or is disconnected
- **THEN** the PowerPoint region displays the corresponding localized status
- **AND** slide navigation is disabled while timer controls remain usable

#### Scenario: Speaker notes are available
- **WHEN** the current slide contains speaker notes
- **THEN** the desktop Control Center indicates that notes are available on the phone presenter without permanently rendering the note body

### Requirement: Control Center manages the existing phone remote session
The phone region SHALL start and end the existing authenticated remote session. When stopped it SHALL present a single start action; when pairing material is available it SHALL make the selected QR code and pairing endpoint controls available; when phones connect it SHALL show the authenticated count and hide the persistent QR behind a Display Pairing QR flyout; ending the session SHALL immediately withdraw pairing material. The desktop SHALL NOT claim a device model, device name, or last-seen time because the current remote protocol does not provide that metadata.

#### Scenario: Remote session is started
- **WHEN** the user activates Start Phone Remote
- **THEN** the phone region shows the existing starting and ready states
- **AND** displays a QR code only after valid desktop pairing material is available

#### Scenario: Multiple network candidates exist
- **WHEN** the running remote session exposes more than one reachable candidate
- **THEN** the phone region allows the user to select a labeled candidate
- **AND** the visible QR code and pairing URL represent the same selected candidate

#### Scenario: A phone connects
- **WHEN** one authenticated phone hub connection is active
- **THEN** the phone region communicates that one device is connected using text plus status indicator
- **AND** it does not invent a phone model, device name, or connection timestamp
- **AND** the QR is no longer permanently displayed in the Control Center

#### Scenario: Pairing QR is requested after connection
- **WHEN** a remote session remains active and the user activates Display Pairing QR
- **THEN** a contextual flyout displays the current selected pairing QR and URL
- **AND** opening the flyout does not rotate the session token or interrupt connected phones

#### Scenario: Remote session ends
- **WHEN** the user activates End Session
- **THEN** the QR code and pairing URL disappear immediately
- **AND** credentials and tokens from that session are invalid under the existing remote security contract

#### Scenario: Remote session cannot start
- **WHEN** no usable LAN address or listener is available
- **THEN** the phone region presents a concise localized failure and retry action
- **AND** the Timer and PowerPoint regions remain operational

### Requirement: Target duration uses presets with validated custom entry
The Control Center SHALL offer 10, 15, 20, and 30 minute presets plus Custom while the timer is Ready. Selecting a preset SHALL configure the existing authoritative timer; Custom SHALL request a positive whole-second duration using the existing accepted duration formats and show validation without losing the prior valid target.

#### Scenario: Preset duration is selected
- **WHEN** the Ready timer user selects the 20 minute preset
- **THEN** the authoritative timer target and both desktop modes display `20:00`
- **AND** the 20 minute choice is visibly selected

#### Scenario: Custom duration is accepted
- **WHEN** the user submits `1:05:30` in the custom-duration dialog
- **THEN** the dialog closes and the Ready timer displays `1:05:30`

#### Scenario: Custom duration is invalid
- **WHEN** the user submits an invalid or unsupported custom duration
- **THEN** a localized validation message remains associated with the dialog input
- **AND** the prior valid timer target remains unchanged

#### Scenario: Timer is not ready
- **WHEN** the timer is Running or Paused
- **THEN** duration presets and custom duration changes are unavailable until Reset returns the timer to Ready

### Requirement: Window behavior matches each desktop mode
Compact Timer SHALL be borderless, low-glare, draggable from non-interactive surface areas, rounded where supported by the operating system, and approximately 440 by 240 effective pixels. Expanded Control Center SHALL provide normal caption actions, be resizable from approximately 920 by 680 effective pixels, and enforce a usable minimum size. Switching modes SHALL be DPI-aware and SHALL restore the Compact window rectangle retained for the current process when that rectangle remains visible on a display.

#### Scenario: Compact window is dragged
- **WHEN** the user drags a non-interactive area of Compact Timer
- **THEN** the operating system moves the window
- **AND** timer buttons remain clickable rather than acting as drag regions

#### Scenario: Window expands and collapses
- **WHEN** the user expands, resizes the Control Center, and then collapses it
- **THEN** Compact Timer restores its prior size and position for the current application session

#### Scenario: Saved compact rectangle is no longer visible
- **WHEN** display topology or DPI changes before Compact Timer is restored
- **THEN** the compact window is clamped to a visible work area instead of reopening off-screen

#### Scenario: Rounded corners are unsupported
- **WHEN** the operating system cannot provide rounded top-level window corners
- **THEN** Compact Timer remains fully usable with square corners

### Requirement: The desktop experience is localized and accessible
All user-facing strings SHALL be available in Simplified Chinese and English. Every icon-only action SHALL have a localized accessible name, tooltip, stable automation identifier, visible keyboard focus, and a touch target suitable for quick presenter use. The surface SHALL remain readable in system Light, Dark, and High Contrast modes, with High Contrast using system colors rather than color-only custom styling.

#### Scenario: Keyboard-only operation
- **WHEN** a user navigates Compact Timer, More, Control Center, custom duration, PowerPoint, and remote actions using only the keyboard
- **THEN** every action can be reached, identified, invoked, and visibly focused in a logical order

#### Scenario: High Contrast is enabled
- **WHEN** Windows High Contrast is active
- **THEN** timer state, controls, focus, and subsystem status remain distinguishable without relying on warning, critical, or success hue alone

#### Scenario: Display language changes
- **WHEN** the application runs in either supported display language
- **THEN** labels, tooltips, menu text, validation, status, and accessible names use that language without clipping at the supported minimum sizes

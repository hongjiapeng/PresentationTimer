## Purpose

Defines the observable Windows desktop workflow that lets a presenter prepare, operate, monitor, and safely end one local presentation session without specialist setup.

## ADDED Requirements

### Requirement: Presentation readiness is visible
The desktop application SHALL show the current timer, presentation, slide-show, remote-session, and phone-connection states in one workspace, using distinct text in addition to color.

#### Scenario: Application starts before PowerPoint
- **GIVEN** Microsoft PowerPoint is not running
- **WHEN** the user starts the desktop application
- **THEN** the countdown workspace is usable
- **AND** the presentation area reports that PowerPoint is not running
- **AND** the application remains ready to detect PowerPoint later

#### Scenario: Slide show becomes ready
- **GIVEN** the application is open and a PowerPoint presentation has no running slide show
- **WHEN** the user starts that slide show in PowerPoint
- **THEN** the desktop presentation area changes to a connected, running state without an application restart
- **AND** it shows the current slide and total slide count

### Requirement: Timer is the visual focus
The desktop application SHALL display the current countdown or overtime value as its most prominent content, together with the configured target duration and controls valid for the current timer state.

#### Scenario: Presenter views the timer from a distance
- **GIVEN** a target duration has been configured
- **WHEN** the workspace is shown at its minimum supported size
- **THEN** the current time remains legible and is not clipped
- **AND** start or pause/resume and reset controls remain accessible

#### Scenario: Overtime is visually unambiguous
- **GIVEN** the running timer has passed zero
- **WHEN** the desktop workspace updates
- **THEN** the overtime value uses an explicit overtime label or positive elapsed notation
- **AND** its styling is visually distinct from normal remaining time

### Requirement: Desktop window supports presentation use
The desktop window SHALL be resizable, enforce a usable minimum size, support light and dark Windows themes, and provide a user-controlled always-on-top setting.

#### Scenario: Always-on-top is enabled
- **GIVEN** the desktop application is not currently always on top
- **WHEN** the user enables always-on-top
- **THEN** the presentation timer remains above ordinary windows until the user disables the setting or exits the application

#### Scenario: Window is resized
- **WHEN** the user resizes the window to any supported size
- **THEN** the timer, primary controls, connection status, and remote-session controls remain reachable without overlapping

### Requirement: Session actions expose clear outcomes
The desktop application SHALL let the user start and end a remote session and SHALL show the resulting server status, selected local URL, QR code availability, and whether at least one authenticated phone is connected.

#### Scenario: Remote session starts successfully
- **GIVEN** the desktop application has at least one usable LAN address
- **WHEN** the user starts a remote session
- **THEN** the workspace reports that the remote is ready
- **AND** it displays a local URL and matching QR code
- **AND** it reports no phone connected until an authenticated browser connects

#### Scenario: Remote session cannot be reached
- **GIVEN** the local listener started but a same-LAN phone cannot connect
- **WHEN** the user opens remote diagnostics
- **THEN** the application presents actionable checks for network equality, Wi-Fi client isolation, selected address, and Windows Firewall
- **AND** it does not claim that the QR code guarantees reachability

### Requirement: Failures degrade individual capabilities
The desktop application SHALL keep unaffected capabilities usable when presentation integration, the remote listener, or a phone connection fails, and SHALL present a user-actionable status instead of terminating unexpectedly.

#### Scenario: PowerPoint exits during a timed talk
- **GIVEN** a timer and remote session are running
- **WHEN** PowerPoint closes unexpectedly
- **THEN** the desktop application continues running
- **AND** the timer continues accurately
- **AND** presentation controls become unavailable with a disconnected status
- **AND** the remote session remains available to show timer and presentation-unavailable state

#### Scenario: Remote listener fails to start
- **GIVEN** the presentation integration and timer are usable
- **WHEN** the remote listener cannot start
- **THEN** the application reports the failure and a retry action
- **AND** local timer and presentation controls remain usable

### Requirement: MVP end-to-end presentation workflow
The desktop application SHALL support a complete presentation flow in which desktop and phone clients converge on the same observable state and an ended remote session cannot be reused.

#### Scenario: Presenter completes a talk with a phone remote
- **GIVEN** the user has started the desktop application, opened a 15-slide PowerPoint presentation, and started its slide show on slide 1
- **WHEN** the application detects PowerPoint, the user configures 15 minutes, starts the timer, starts a remote session, and scans its QR code on an iPhone or Android phone
- **THEN** the desktop and phone show slide 1 of 15, the current speaker notes, and the remaining time
- **WHEN** the user selects Next on the phone
- **THEN** PowerPoint advances to slide 2 and both clients update to slide 2 of 15 with its speaker notes
- **WHEN** the user changes slides directly through PowerPoint, a keyboard, or a presentation clicker
- **THEN** both clients update without a manual refresh
- **WHEN** the timer reaches zero
- **THEN** both clients continue into a clearly identified overtime display
- **WHEN** the user ends the remote session
- **THEN** the phone loses remote access and the old QR code can no longer authorize a connection


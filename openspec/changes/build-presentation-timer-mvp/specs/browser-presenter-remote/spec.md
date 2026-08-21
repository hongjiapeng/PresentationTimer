## Purpose

Defines a zero-install, touch-first phone browser experience for viewing authoritative presenter information and safely controlling the active slide show over the local network.

## ADDED Requirements

### Requirement: Valid pairing opens a zero-install presenter
An iPhone or Android browser authorized by the active QR code SHALL open the presenter directly without requiring a native app, account, cloud service, Internet connection, or manual server address entry.

#### Scenario: Phone scans the current QR code
- **GIVEN** the phone and PC can reach each other on the same LAN and a remote session is active
- **WHEN** the phone scans the current QR code
- **THEN** its browser opens the presenter interface
- **AND** no app installation, login, or Internet service is requested

#### Scenario: Phone opens an old QR code
- **GIVEN** the QR code belongs to an ended session
- **WHEN** the phone opens it
- **THEN** the page shows that the session is invalid or expired
- **AND** presenter data and controls remain unavailable

### Requirement: Presenter information is glanceable
The phone presenter SHALL show current slide and total slide count, current speaker notes, remaining time or overtime, and connection state in a responsive layout that remains usable at common phone viewport sizes and orientations.

#### Scenario: Running presentation state is received
- **GIVEN** PowerPoint is showing slide 7 of 18 with speaker notes and the timer has 8 minutes 42 seconds remaining
- **WHEN** the phone receives the current state
- **THEN** it shows slide 7 of 18, the current notes, and 08:42 remaining
- **AND** the primary information is visible without horizontal scrolling

#### Scenario: Presentation is temporarily unavailable
- **GIVEN** the remote session and timer are active but no slide show is running
- **WHEN** the phone receives the current state
- **THEN** it shows the timer and a clear presentation-not-ready state
- **AND** slide navigation controls are disabled

### Requirement: Navigation is touch-first
The phone presenter SHALL provide Previous and Next controls with large touch targets suitable for one-handed operation and SHALL prevent unsupported commands while disconnected, unauthorized, or without a running slide show.

#### Scenario: User taps Next once
- **GIVEN** the phone is connected and slide 3 is active
- **WHEN** the user taps Next once
- **THEN** exactly one next command is submitted
- **AND** the phone displays the resulting authoritative slide state rather than guessing the next slide locally

#### Scenario: User taps while disconnected
- **GIVEN** the phone has lost its connection
- **WHEN** the user taps Previous or Next
- **THEN** no command is represented as successful
- **AND** the interface continues to show the disconnected state

### Requirement: State updates are real time and authoritative
The phone presenter SHALL update slide position, speaker notes, timer state, overtime, and presentation availability from server-published authoritative state without a manual page refresh.

#### Scenario: Slide changes outside the phone
- **GIVEN** the phone shows slide 5
- **WHEN** the slide changes from PowerPoint, a keyboard, a clicker, or the desktop application
- **THEN** the phone updates its slide position and notes automatically

#### Scenario: Timer enters overtime
- **GIVEN** the phone shows one second remaining
- **WHEN** the authoritative timer passes zero
- **THEN** the phone continues updating with a clearly identified overtime value

### Requirement: Disconnect and reconnect are explicit
The phone presenter SHALL visibly distinguish connected, reconnecting, disconnected, invalid-session, and expired-session states and SHALL automatically retry transient disconnections with bounded backoff while the session remains potentially valid.

#### Scenario: Wi-Fi briefly drops
- **GIVEN** the phone is connected to an active session
- **WHEN** network reachability is temporarily lost
- **THEN** the presenter immediately indicates reconnecting or disconnected
- **AND** it attempts to reconnect without a page refresh

#### Scenario: Automatic reconnect succeeds
- **GIVEN** the phone missed one or more state changes while disconnected
- **WHEN** it reconnects to the same active session
- **THEN** it requests and renders a complete current state snapshot before re-enabling navigation
- **AND** stale slide notes and timer values are replaced

#### Scenario: Session ends while reconnecting
- **GIVEN** the phone is reconnecting when the desktop user ends the session
- **WHEN** the phone next reaches the PC
- **THEN** it changes to an expired-session state
- **AND** automatic command retries stop

### Requirement: Speaker notes are rendered safely
The phone presenter SHALL render speaker notes as plain text, preserve meaningful line breaks, and SHALL NOT execute markup or script contained in note text.

#### Scenario: Notes contain markup-like text
- **GIVEN** the current speaker notes include HTML or script-like characters
- **WHEN** the notes are displayed on the phone
- **THEN** those characters appear as note text
- **AND** no embedded markup or script is executed

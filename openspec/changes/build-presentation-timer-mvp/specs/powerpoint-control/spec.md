## Purpose

Defines safe observation and navigation of the user's currently active Microsoft PowerPoint slide show, including slide position, speaker notes, external changes, and loss or recovery of PowerPoint.

## ADDED Requirements

### Requirement: PowerPoint readiness states are distinguished
The system SHALL distinguish and report at least these states: PowerPoint unavailable or not installed, PowerPoint installed but not running, PowerPoint running with no open presentation, presentation open with no running slide show, running slide show, and integration disconnected after an error.

#### Scenario: PowerPoint is not installed
- **GIVEN** Microsoft PowerPoint is not installed or registered for the current Windows user
- **WHEN** the application checks presentation readiness
- **THEN** it reports that PowerPoint is unavailable
- **AND** it does not terminate or disable the timer

#### Scenario: Presentation is open without a slide show
- **GIVEN** PowerPoint is running with an active presentation
- **WHEN** no slide show is running
- **THEN** the application reports that the presentation is open but the slide show has not started
- **AND** slide-show navigation controls are unavailable

### Requirement: Running PowerPoint is detected and re-detected
The system SHALL detect a running PowerPoint instance and its active slide show without requiring the application to launch, open, edit, or take ownership of a presentation, and SHALL continue looking after PowerPoint becomes unavailable.

#### Scenario: PowerPoint starts after the application
- **GIVEN** the application is open and reports that PowerPoint is not running
- **WHEN** the user starts PowerPoint, opens a presentation, and starts a slide show
- **THEN** the application connects and displays the running slide show without an application restart

#### Scenario: PowerPoint restarts
- **GIVEN** the application previously lost a PowerPoint connection
- **WHEN** the user restarts PowerPoint and starts a slide show
- **THEN** the application establishes a fresh connection
- **AND** stale data and controls from the old PowerPoint instance are not reused

### Requirement: Current slide state is exposed
While a slide show is running, the system SHALL expose the current slide's presentation index, the active presentation's total slide count, and the speaker-notes plain text associated with that current slide.

#### Scenario: Slide contains speaker notes
- **GIVEN** the current slide is slide 7 in an 18-slide presentation and contains speaker notes
- **WHEN** presentation state is read
- **THEN** the state reports current slide 7 and total slides 18
- **AND** it reports the speaker-note text without slide artwork, headers, footers, dates, or slide numbers

#### Scenario: Slide has no speaker notes
- **GIVEN** the current slide has no speaker-note text
- **WHEN** presentation state is read
- **THEN** the slide position is still reported
- **AND** speaker notes are reported as empty rather than as an error

### Requirement: Presenter can navigate the running slide show
The system SHALL accept previous and next commands only for the currently active running slide show and SHALL report the resulting authoritative state after PowerPoint processes the command.

#### Scenario: Next advances the slide show
- **GIVEN** the active slide show is on slide 1 of 15
- **WHEN** an authorized desktop or phone client requests Next
- **THEN** PowerPoint advances according to its slide-show behavior
- **AND** the resulting current slide and speaker notes are published

#### Scenario: Navigation is requested without a slide show
- **GIVEN** PowerPoint has no running slide show
- **WHEN** a client requests Next or Previous
- **THEN** the command does not start a slide show or modify the presentation
- **AND** the client receives a presentation-not-ready result

#### Scenario: Next is requested on the last slide
- **GIVEN** PowerPoint is displaying the final slide
- **WHEN** a client requests Next
- **THEN** PowerPoint's configured end-of-show behavior is preserved
- **AND** the application reports the resulting slide-show state, including a stopped state if the show ends

### Requirement: External slide-show changes are synchronized
The system SHALL observe slide-show start, slide changes, and slide-show end that originate in PowerPoint, from a keyboard, or from another presentation controller, and SHALL publish the resulting full presentation state without manual refresh.

#### Scenario: User advances with a keyboard
- **GIVEN** desktop and phone clients show slide 4 of 12
- **WHEN** the user advances the slide show with a keyboard or clicker
- **THEN** both clients update to the new current slide and its notes

#### Scenario: Slide show ends in PowerPoint
- **GIVEN** the application is connected to a running slide show
- **WHEN** the user ends the slide show directly in PowerPoint
- **THEN** the application reports that the slide show is no longer running
- **AND** navigation controls become unavailable

### Requirement: PowerPoint loss never crashes the application
The system SHALL contain failures caused by closed, busy, restarted, or invalid PowerPoint objects, clear any state that can no longer be trusted, and preserve timer and remote-session operation.

#### Scenario: PowerPoint closes during a state read
- **GIVEN** the application is reading presentation state
- **WHEN** PowerPoint closes or rejects the operation
- **THEN** the application reports a disconnected presentation state
- **AND** it does not crash, hang indefinitely, or expose the previous slide as current

#### Scenario: Application shuts down while subscribed to PowerPoint
- **GIVEN** the application is observing a running PowerPoint slide show
- **WHEN** the user exits the application
- **THEN** the application stops observing PowerPoint and releases its connection
- **AND** the user's PowerPoint process and presentation remain open


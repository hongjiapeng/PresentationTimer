## Purpose

Defines an accurate, pauseable presentation countdown whose state remains authoritative across UI stalls and continues visibly into overtime instead of stopping at zero.

## ADDED Requirements

### Requirement: Presenter can configure a target duration
The system SHALL accept a positive presentation duration with whole-second precision while the timer is in its ready state and SHALL reject zero, negative, malformed, or unsupported values with a clear validation message.

#### Scenario: Valid duration is configured
- **GIVEN** the timer is ready and not running
- **WHEN** the user configures a duration of 15 minutes
- **THEN** the target and remaining time are both 15 minutes
- **AND** the timer is ready to start

#### Scenario: Invalid duration is rejected
- **GIVEN** the timer is ready
- **WHEN** the user enters zero, a negative value, or non-time text
- **THEN** the timer does not start or change its last valid target
- **AND** the application explains that a positive duration is required

### Requirement: Timing remains accurate across delayed updates
The system SHALL derive remaining or overtime from actual elapsed duration so timing accuracy is independent of UI or network refresh frequency and changes to the system wall clock.

#### Scenario: UI updates are delayed
- **GIVEN** a 15-minute timer has been running for 10 minutes
- **WHEN** desktop display updates are blocked for 20 seconds and then resume
- **THEN** the reported timer value reflects approximately 4 minutes 40 seconds remaining
- **AND** it does not lose the 20 seconds during which the display was blocked

#### Scenario: Display refresh rates differ
- **GIVEN** desktop and phone displays refresh at different intervals
- **WHEN** both render the same authoritative running timer state
- **THEN** their displayed whole-second values differ by no more than one display tick under normal network conditions

### Requirement: Timer supports pause and resume
The system SHALL stop accumulating elapsed time while paused and SHALL continue from the preserved value when resumed, including when the timer is already in overtime.

#### Scenario: Countdown is paused and resumed
- **GIVEN** a 15-minute timer has 8 minutes remaining
- **WHEN** the user pauses it for 30 seconds
- **THEN** it continues to report 8 minutes remaining throughout the pause
- **WHEN** the user resumes it
- **THEN** elapsed time accumulation continues from 8 minutes remaining

#### Scenario: Overtime is paused
- **GIVEN** the timer is running 2 minutes overtime
- **WHEN** the user pauses it
- **THEN** the overtime value remains at 2 minutes until resumed or reset

### Requirement: Reset restores the configured target
The system SHALL allow reset from ready, running, paused, or overtime state and SHALL return the timer to a non-running ready state at the configured target duration.

#### Scenario: Running timer is reset
- **GIVEN** a 15-minute timer has 3 minutes remaining
- **WHEN** the user resets it
- **THEN** the timer stops and reports 15 minutes remaining
- **AND** it does not begin accumulating elapsed time until started again

#### Scenario: Overtime timer is reset
- **GIVEN** a timer reports 4 minutes overtime
- **WHEN** the user resets it
- **THEN** the overtime indication clears
- **AND** the configured target duration is restored

### Requirement: Timer continues through zero
The system SHALL transition a running countdown through zero into an increasing overtime value without automatically stopping.

#### Scenario: Countdown crosses zero
- **GIVEN** a running timer has one second remaining
- **WHEN** more than one second of elapsed time passes
- **THEN** the timer remains running
- **AND** it reports the amount of overtime with an unambiguous overtime indication

### Requirement: All clients receive authoritative timer state
The system SHALL publish timer target, run state, remaining or overtime value, and state transitions to every connected client, and SHALL provide the current full timer state to a newly connected or reconnected client.

#### Scenario: Timer is controlled from the desktop
- **GIVEN** an authenticated phone is connected
- **WHEN** the desktop user starts, pauses, resumes, or resets the timer
- **THEN** the phone updates to the corresponding timer state without manual refresh

#### Scenario: Phone reconnects during a running timer
- **GIVEN** the timer continued running while the phone was disconnected
- **WHEN** the phone reconnects
- **THEN** it receives the current timer state based on total elapsed time
- **AND** it does not resume from the stale pre-disconnect display value

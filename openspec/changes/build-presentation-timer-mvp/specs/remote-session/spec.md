## Purpose

Defines the lifecycle, LAN reachability, QR pairing, authorization boundary, and revocation behavior for a temporary browser-based presenter remote session.

## ADDED Requirements

### Requirement: Remote listener is session-scoped
The system SHALL start LAN remote service only after the user starts a remote session and SHALL stop serving presenter data and commands when that session ends or the desktop application exits.

#### Scenario: User starts a remote session
- **GIVEN** no remote session is active
- **WHEN** the user starts a remote session
- **THEN** the application starts a local listener on a non-privileged available port
- **AND** reports its starting, ready, or failed status

#### Scenario: User ends a remote session
- **GIVEN** a remote session is active
- **WHEN** the user ends the session
- **THEN** remote presenter connections are closed or denied
- **AND** presentation state, speaker notes, and navigation commands are no longer served by that session

### Requirement: QR pairing uses a fresh high-entropy token
The system SHALL generate a cryptographically secure, unguessable token with at least 128 bits of entropy for each new remote session and SHALL encode the selected current LAN URL and token in the QR code.

#### Scenario: First session is started
- **WHEN** the user starts a remote session
- **THEN** the displayed QR code decodes to the displayed presenter URL with that session's token
- **AND** token generation does not depend on predictable time, process, user, or device values

#### Scenario: A new session follows an ended session
- **GIVEN** one remote session has ended
- **WHEN** the user starts another remote session
- **THEN** a different token is generated
- **AND** the previous QR code does not authorize the new session

### Requirement: Every remote operation is authorized
The system SHALL validate the current session credential before returning presenter state or speaker notes, opening a real-time connection, or accepting a presentation command, and SHALL disclose no protected presentation data when validation fails.

#### Scenario: Valid QR is used
- **GIVEN** a remote session is active
- **WHEN** a browser opens the current QR URL with its valid token
- **THEN** the browser is authorized only for that active session
- **AND** may receive presenter state and issue supported presenter commands

#### Scenario: Token is invalid
- **GIVEN** a remote session is active
- **WHEN** a browser supplies a missing, malformed, or incorrect token
- **THEN** access is denied with a clear invalid-session response
- **AND** no speaker notes, timer state, slide state, or command result is disclosed

#### Scenario: Token is expired by session end
- **GIVEN** a browser was authorized for a session that has ended
- **WHEN** it reconnects, requests state, or sends a command
- **THEN** access is denied as expired
- **AND** the user is instructed to scan a new QR code

### Requirement: LAN addresses are explicit and local
The system SHALL enumerate usable non-loopback LAN addresses, allow the user to select among multiple candidates, generate pairing information locally, and clearly distinguish a diagnostic localhost address from a phone-reachable address.

#### Scenario: Multiple network adapters are present
- **GIVEN** the PC has multiple usable LAN IPv4 addresses
- **WHEN** a remote session becomes ready
- **THEN** the application lists distinct candidate presenter URLs
- **AND** the selected visible URL exactly matches the QR code content

#### Scenario: No usable LAN address exists
- **GIVEN** only loopback or otherwise unusable addresses are available
- **WHEN** the user starts a remote session
- **THEN** the application explains that another device cannot currently connect
- **AND** it does not present localhost as a phone-reachable QR code

### Requirement: Network changes refresh pairing information
The system SHALL detect when available LAN addresses change during an active session and SHALL refresh or withdraw the displayed URL and QR code so they do not claim a stale address is reachable.

#### Scenario: PC changes Wi-Fi network
- **GIVEN** a remote session is active on one LAN address
- **WHEN** that address disappears and a new usable LAN address appears
- **THEN** the desktop application updates the candidate and selected presenter URLs
- **AND** it refreshes the QR code for the live session
- **AND** already disconnected phones receive a clear reconnecting or disconnected state rather than stale presentation data

### Requirement: Firewall and isolation failures are user-controlled
The system SHALL NOT silently elevate privileges, create or change firewall rules, disable firewall protection, or change Windows network settings, and SHALL provide user-executable diagnostics when the listener is locally healthy but unreachable from a phone.

#### Scenario: Windows requests firewall consent
- **GIVEN** the user starts the first LAN remote session
- **WHEN** Windows displays a firewall consent prompt
- **THEN** the application allows the user or administrator to decide
- **AND** the application does not bypass or answer the prompt automatically

#### Scenario: Managed PC blocks inbound access
- **GIVEN** local health checks pass but same-LAN phone access is blocked by policy
- **WHEN** remote diagnostics are shown
- **THEN** the application explains that an administrator may need to permit the app on a private/local-subnet network
- **AND** local timer and PowerPoint functions remain available

### Requirement: LAN-only HTTP risk is bounded and visible
The MVP SHALL advertise remote service only on local interface addresses, SHALL never claim that its HTTP session encrypts LAN traffic, and SHALL minimize exposure of the session token in browser navigation and application logs.

#### Scenario: User reviews remote security information
- **WHEN** the user opens remote-session security information
- **THEN** the application explains that anyone who can observe or obtain the active QR token on the LAN could control the presentation and read notes until the session ends
- **AND** it recommends using a trusted local network and ending the session immediately after the talk

#### Scenario: Authorized landing completes
- **GIVEN** a browser opens a valid token-bearing QR URL
- **WHEN** authorization succeeds
- **THEN** subsequent presenter navigation uses a token-free visible URL where browser capabilities permit
- **AND** normal application logging does not record the token value


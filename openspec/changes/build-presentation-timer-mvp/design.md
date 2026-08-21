## Context

This is a greenfield repository; before this change it contained no application code, solution conventions, architecture documents, or product specs. OpenSpec has been initialized with the default `spec-driven` schema. See `proposal.md` for product motivation and the five delta specs for observable behavior.

The application combines four concurrency domains with different failure modes:

1. a WinUI UI thread;
2. elapsed-time domain logic;
3. an Office COM object model that requires disciplined apartment and reference lifetime handling;
4. an in-process LAN web host with concurrent browser connections.

The dominant design constraint is isolation: a PowerPoint failure must not corrupt the timer or web host, a slow phone must not block COM, and no COM runtime callable wrapper (RCW) may escape the PowerPoint integration boundary. The MVP is local-first, has no persistent session data, and must remain simple enough to dogfood early.

Research informed but did not supply application code. `PhoneAsPrompter` validates the basic browser/Kestrel/PowerPoint concept, but its unauthenticated `/next` and `/previous` endpoints, 300 ms polling, UI-thread COM calls, port 80 listener, PowerPoint `Quit`, and forced process termination are explicitly rejected. `Zack.ComObjectHelpers` illustrates explicit RCW tracking, but it will not be referenced or copied because of its GPL license and because indiscriminate `FinalReleaseComObject` use is unsafe when RCWs are shared. The local `PhoneControlKit` project validates static browser assets, local QR generation, adapter-aware URL display, and the need to treat QR generation separately from actual LAN reachability.

## Goals / Non-Goals

**Goals:**

- Keep timer, PowerPoint, remote host, and UI independently failure-tolerant.
- Establish one immutable, revisioned presentation-session snapshot as the state read by desktop and browser clients.
- Constrain all PowerPoint COM creation, access, events, and release to one dedicated STA thread with a working message pump.
- Make remote authorization ephemeral, revocable, local-only, and understandable without introducing accounts or a general identity system.
- Reach a real phone-to-PowerPoint vertical slice early and keep every implementation stage buildable.
- Produce deterministic unit/integration tests where practical and a small, realistic manual matrix for Office automation.

**Non-Goals:**

- A general plugin/provider framework, dependency injection framework beyond the built-in .NET host, message bus, CQRS layer, database, or persistent event log.
- Starting PowerPoint, opening files, starting slide shows, changing slide content, or calling `PowerPoint.Application.Quit`.
- Native mobile code, service workers/PWA installation, Internet relay, NAT traversal, automatic TLS certificates, or cloud identity.
- Supporting more than the currently active PowerPoint slide show in the MVP.
- NativeAOT, ARM64 distribution, Microsoft Store submission, auto-update, or enterprise firewall deployment in the first dogfood package.

## Assumptions

- The primary dogfood environment is Windows 11 x64 with desktop Microsoft PowerPoint; the project targets `net10.0-windows10.0.19041.0` unless implementation-time SDK constraints require a higher Windows target.
- Modern iOS Safari and Android Chrome are the supported phone browsers. The phone and PC have direct IP reachability over the same trusted LAN/Wi-Fi; IPv4 is the MVP path.
- One PowerPoint application and one active slide show are authoritative. If PowerPoint exposes multiple slide shows, the currently active slide-show window wins; slide-show selection UI is deferred.
- Displayed current slide is `Slide.SlideIndex`, total slides is the active presentation's `Slides.Count`, and native PowerPoint navigation behavior is preserved for hidden slides, custom shows, and the final slide.
- Speaker notes are plain text from notes body placeholders. Rich formatting, images, ink, and embedded objects are not transferred.
- A remote session is in memory only. Multiple browsers holding the same live QR token may connect; desktop “phone connected” means one or more authenticated hub connections.
- Timer and session state do not survive application exit. Settings persistence is unnecessary for the MVP and can be added later without changing the behavior contract.
- LAN HTTP is acceptable only for trusted-network dogfood with the threat boundary documented below.

## Decisions

### 1. Overall architecture and dependency direction

Use the proposed four-project split plus two test projects:

```text
PresentationTimer.sln
|
+-- src/PresentationTimer.App          WinUI views, view models, composition root
|       |\
|       | +---------------------> PresentationTimer.PowerPoint
|       +-----------------------> PresentationTimer.Remote
|       +-----------------------> PresentationTimer.Core
|
+-- src/PresentationTimer.Core         domain state, timer, contracts, coordinator
|       ^                         (no UI, Office, ASP.NET, or QR references)
|       |
+-- src/PresentationTimer.PowerPoint   Office adapter + dedicated STA dispatcher
|       +------------------------> Core contracts only
|
+-- src/PresentationTimer.Remote       local host, auth, hub, static phone UI, QR
|       +------------------------> Core contracts only
|
+-- tests/PresentationTimer.Core.Tests
+-- tests/PresentationTimer.Remote.Tests
```

`PresentationTimer.App` is the composition root and owns process startup/shutdown. `Core` does not reference any infrastructure project. Both infrastructure projects implement interfaces defined in `Core`. `Remote` never references `PowerPoint`; browser commands go through the same application service as desktop commands. The UI never receives a COM object and never calls Office APIs.

This keeps the suggested provider seam (`IPresentationController`) without adding a registry, provider discovery, or unused abstractions. A future provider can implement the same small contract, but the MVP constructs exactly one PowerPoint adapter.

Alternatives rejected:

- A single WinUI project would reduce project count but makes it too easy for COM calls to leak into view models and for the web host to depend on UI state.
- A separate remote executable would improve process isolation but complicates state IPC, startup, installation, and shutdown before dogfood has proved the need.
- A generic event bus or mediator adds indirection without an MVP use case.

### 2. Main contracts and application service

Keep contracts narrow and asynchronous at infrastructure boundaries. Names can be refined during implementation, but responsibilities are fixed:

```text
IPresentationController
  State: PresentationSnapshot
  StartMonitoringAsync / StopMonitoringAsync
  NextAsync / PreviousAsync
  StateChanged(PresentationSnapshot)

IPresentationTimer
  State: TimerSnapshot
  Configure / Start / Pause / Resume / Reset
  Snapshot(now)
  StateChanged(TimerSnapshot)

IRemoteSessionHost
  StartAsync -> DesktopPairingDescriptor
  StopAsync
  StateChanged(RemoteSessionPublicState)

IPresentationSessionService
  State: PresentationSessionState
  timer commands
  presentation commands
  StartRemoteSessionAsync / EndRemoteSessionAsync
  StateChanged(PresentationSessionState)
```

`PresentationSessionService` is the only application command gateway. WinUI view models and SignalR hub methods both call it. It subscribes to timer, presentation-controller, and remote-host state events, merges the changed slice into the immutable session snapshot under a short lock, increments a `Revision`, then publishes the new snapshot after releasing the lock. No external I/O or event callback runs while the state lock is held.

Commands that require infrastructure execute outside the state lock. The resulting adapter snapshot/event is then merged. PowerPoint navigation is not optimistically applied: the UI and phone wait for PowerPoint's resulting authoritative snapshot. Browser navigation invocations are never automatically retried because Next/Previous are non-idempotent; an uncertain invocation waits for state reconciliation.

### 3. Authoritative presentation state

Use immutable records/value objects with explicit sub-states rather than mutable view-model objects:

```text
PresentationSessionState
  Revision: long
  ObservedAtUtc: DateTimeOffset           diagnostics only
  Presentation: PresentationSnapshot
  Timer: TimerSnapshot
  Remote: RemoteSessionPublicState

PresentationSnapshot
  Connection: Unavailable | NotRunning | NoPresentation |
              NoSlideShow | Running | Disconnected
  CurrentSlideIndex: int?
  TotalSlides: int?
  SpeakerNotes: string
  LastErrorCode: string?

TimerSnapshot
  RunState: Ready | Running | Paused
  Target: TimeSpan
  Remaining: TimeSpan                     signed internally
  IsOvertime: bool

RemoteSessionPublicState
  Status: Stopped | Starting | Ready | Failed | Stopping
  CandidateUrls: read-only list           token-free
  SelectedUrl: Uri?                       token-free
  AuthenticatedConnectionCount: int
  LastErrorCode: string?
```

`IsOvertime` is derived from signed remaining time and is intentionally not a mutually exclusive run state; overtime can be running or paused. Component updates merge one slice so a PowerPoint event cannot overwrite a newer timer value. Equal non-timer snapshots are not rebroadcast. Whole-second timer changes are published while running.

The desktop-only `DesktopPairingDescriptor` contains the token-bearing URI and QR payload. It is never part of the shared state DTO and is never sent to a phone. `PresenterStateDto` is an explicit projection containing only presentation, timer, revision, and connection-safe status fields; serializing the internal state object directly is forbidden.

### 4. Timer model and refresh cadence

Use `Stopwatch.GetTimestamp()` through a small `IMonotonicClock` interface. The timer stores:

- configured target duration;
- accumulated elapsed duration from completed run segments;
- monotonic timestamp at the start of the current run segment, when running.

At any read, elapsed time is `accumulated + clock.GetElapsedTime(runStart, now)` and signed remaining time is `target - elapsed`. Pause folds the current segment into accumulated elapsed; resume records a new run-start timestamp; reset clears accumulated elapsed. No wall-clock value participates in accuracy, so daylight-saving, NTP, or manual clock changes have no effect.

A periodic notifier exists only to ask for a fresh computed snapshot. It does not decrement state. The desktop may refresh at 100–250 ms for a smooth zero crossing while the remote broadcaster emits at most once per displayed second plus immediately on transitions. If the notifier or UI stalls, the next snapshot jumps to the correct value. Tests use a fake monotonic clock and never wait in real time.

The browser renders received whole-second values and may use `performance.now()` only to interpolate the display between authoritative heartbeats. It replaces that interpolation on every server snapshot and after reconnect; only the server decides pause, overtime, and session state.

### 5. PowerPoint adapter and attachment strategy

Use early-bound `Microsoft.Office.Interop.PowerPoint` types with embedded interop metadata. Avoid `dynamic`, reflection chains, and source-generated/custom COM wrappers in the MVP.

The adapter never creates PowerPoint. It checks whether `PowerPoint.Application` is registered and attaches to the running application through the native OLE `GetActiveObject` function. Modern .NET does not provide the old .NET Framework convenience API consistently, so the small P/Invoke wrapper is isolated and covered by result-code tests where possible. If no running object is registered, the adapter reports `NotRunning` and retries attachment on a low-frequency reconciliation schedule.

On attachment it subscribes to the application events needed to invalidate state:

- `SlideShowBegin`;
- `SlideShowNextSlide`;
- `SlideShowEnd`;
- `AfterPresentationOpen`;
- `PresentationClose`;
- `WindowActivate` where available for active-presentation changes.

PowerPoint documents `SlideShowNextSlide` as firing immediately before the transition. Therefore an event callback does not publish guessed slide numbers. It posts a refresh operation back to the STA queue so state is read after the callback/transition, with one short deferred reconciliation if the view still reports the old slide. A 2-second reconciliation pass remains active as a safety net for missed events, late attachment, PowerPoint restart, and external changes. Events are the fast path; polling is recovery, not the state model.

To read state, the adapter takes one short-lived snapshot of primitives. It uses the active slide-show window's `View.Slide`, `Slide.SlideIndex`, its parent presentation's slide count, and the notes page body/vertical-body placeholders. It extracts `TextFrame.TextRange.Text`, normalizes line endings, preserves meaningful line breaks, and excludes slide image, title/header/footer/date/slide-number placeholders. A missing notes body is an empty string.

### 6. COM threading model and state marshaling

All PowerPoint COM work runs on one dedicated background STA thread owned by `PresentationTimer.PowerPoint`; the WinUI UI STA is not used for Office automation.

The STA worker:

1. sets apartment state to STA before start and initializes OLE/COM on that thread;
2. creates a Windows message queue;
3. runs a `GetMessage`/`DispatchMessage` loop so COM connection-point callbacks can be delivered;
4. accepts work through a thread-safe queue woken by a private `WM_APP` thread message;
5. completes `TaskCompletionSource` results asynchronously back to callers;
6. posts `WM_QUIT` only after event unsubscription and RCW cleanup.

This is a small dispatcher, not a message bus: it serializes only COM apartment work. Raw waits, `.Result`, `.Wait()`, `Thread.Sleep`, and arbitrary async continuations are forbidden on the STA thread because they can stop the COM message pump. Delayed retries use a timer that posts a new work item.

COM event callbacks perform minimal work: mark state dirty and post a snapshot refresh. The only values leaving the STA are immutable managed primitives/strings and structured error codes. Event arguments, application, presentation, window, view, slide, notes page, shapes, placeholders, and text-range RCWs never cross to Core, SignalR, view models, or another thread.

### 7. COM object lifetime and failure recovery

The adapter owns one long-lived `PowerPoint.Application` RCW only while attached because application events require it. Every nested object is acquired into a per-operation COM scope and released deterministically in reverse acquisition order in `finally`. The scope tracks RCWs by managed reference identity so the same wrapper is not released twice. Long property chains are broken into named locals; enumeration uses indexed access and releases each item before moving on.

`Marshal.FinalReleaseComObject` is allowed only for objects exclusively owned by this private STA adapter and never for an event argument or a wrapper shared with another scope. The root application is released only after all delegates have been unsubscribed. The adapter never invokes `Application.Quit`, closes a presentation, runs a slide show, or kills a process.

Expected COM failures are mapped to stable states/codes:

- class not registered -> `Unavailable`;
- running object unavailable -> `NotRunning`;
- no presentations -> `NoPresentation`;
- no slide-show windows -> `NoSlideShow`;
- call rejected/busy -> bounded delayed retry, then a transient busy result;
- disconnected RPC/server unavailable/invalid RCW -> unsubscribe best effort, release owned references, publish `Disconnected`, and return to attach polling.

On any unexpected exception, stale slide index and notes are cleared before publishing; raw exception text is logged locally with token-safe structured logging but not returned to a phone.

### 8. Local remote host and SignalR model

`PresentationTimer.Remote` builds an in-process ASP.NET Core host only when a user starts a remote session. It serves:

- a token exchange/landing endpoint;
- local static HTML/CSS/JavaScript assets;
- one authenticated presenter hub;
- a loopback health endpoint containing no presentation data.

There are no unauthenticated command URLs. Hub methods are limited to `GetState`, `Next`, and `Previous`; timer controls remain desktop-only for the MVP. The hub is thin and calls `IPresentationSessionService`. It contains no presentation state and no PowerPoint reference.

On an authenticated hub connection, the server increments connection count and sends a complete `PresenterStateDto`. Each subsequent state broadcast carries `Revision`; clients ignore an older revision. On disconnect, the count is decremented safely. Next/Previous return a structured result, but all clients still converge through the broadcast full snapshot.

The official SignalR JavaScript client is bundled locally with its license and pinned version; the phone UI has no CDN, npm-at-runtime, or frontend framework. Static HTML/CSS/JavaScript is sufficient. Speaker notes are inserted with `textContent`, not `innerHTML`.

Alternatives rejected:

- Plain polling is simpler but increases latency/load and makes disconnect state less clear.
- Raw WebSockets require recreating reconnection, invocation, and serialization behavior already provided by SignalR.
- Stateful reconnect buffering is unnecessary for tiny full-state snapshots; an explicit `GetState` after reconnect is easier to reason about.

### 9. Server and LAN address lifecycle

At session start, enumerate operational IPv4 unicast addresses from non-loopback interfaces and retain interface name/prefix metadata. Do not guess which Wi-Fi, Ethernet, VPN, or virtual adapter can reach the phone. The host binds only loopback and the eligible interface addresses rather than `0.0.0.0`, using non-privileged OS-assigned ports; it then reads the actual bound endpoints and presents distinct labeled candidate URLs. A locally generated QR always matches the selected token-bearing URL.

If a candidate endpoint cannot bind, omit it and report the endpoint error while retaining any healthy endpoint. If none bind, the session enters `Failed` and can be retried without affecting timer or PowerPoint. A loopback health check distinguishes “server failed” from “LAN path/firewall failed.” Localhost is diagnostic only and never presented as phone-reachable.

Network-address change notifications trigger debounced re-enumeration. If an endpoint disappears or a new address cannot be added to the running host, restart the remote host with the same active session credential store, refresh the URL/QR list, and mark existing clients disconnected/reconnecting. The presenter may need to rescan; the application never claims transparent recovery across a PC address change.

### 10. QR token, browser credential, and revocation

On each `Start Remote Session`:

1. generate 32 random bytes with a cryptographically secure RNG;
2. encode them as an unpadded base64url pairing token;
3. keep only a SHA-256 hash in the session credential store, comparing in fixed time;
4. place the raw pairing token only in the desktop pairing URI/QR descriptor;
5. clear the descriptor and credential store on session end.

The QR opens `/pair?t=<pairing-token>`. The endpoint validates the token, creates a separate random per-browser credential, stores only its hash, sets it in an `HttpOnly; SameSite=Strict; Path=/` session cookie, and redirects to the token-free `/presenter` URL. `Secure` cannot be set for the HTTP MVP. The page sets `Referrer-Policy: no-referrer`; release request logging excludes query strings and raises framework request-start logging above Information so the pairing token is not written to normal logs.

The same-origin cookie authorizes static presenter entry, hub negotiation/connection, state reads, and commands through a small custom authentication handler. CORS is not opened. WebSocket Origin is checked against current local origins as defense in depth, never as authentication.

Ending a session first marks credentials invalid, then aborts/stops hub connections and the host. Starting again creates a new pairing token and new port(s), so an old QR, cookie, and connection cannot regain access. No JWT, password, account, refresh token, or permanent secret is introduced.

### 11. HTTP threat model and security boundary

Protected assets are slide navigation authority, speaker notes, slide position, and timer state. The assumed adversary may scan the PC's open LAN port or guess URLs but does not already control the PC or phone.

Mitigations:

- 256-bit session tokens make guessing infeasible;
- every state/command path, including SignalR, requires the live credential;
- the listener exists only for an explicit session and binds only selected local interfaces;
- pairing tokens are removed from the visible URL after exchange and redacted from logs;
- cookie scope, same-origin hosting, SameSite, no-referrer, and Origin checks reduce cross-site leakage/commands;
- ending a session revokes all credentials immediately.

Accepted MVP trade-off: HTTP does not protect against a passive or active attacker already able to observe/modify traffic on the LAN, a camera/history leak of the live QR, a compromised authorized phone, or a malicious local administrator. Such an actor can steal a bearer credential, read notes, or control slides until session end. The UI must communicate “trusted local network only”; production use on hostile/public Wi-Fi requires a later authenticated-encryption design, not a claim that the current token provides confidentiality.

### 12. Browser connection lifecycle

The phone registers state handlers before starting its hub connection. Connection UI states are `connecting`, `connected`, `reconnecting`, `disconnected`, and `expired`.

Use automatic reconnect with immediate first retry and capped exponential delays up to 30 seconds. A custom policy may continue at the cap while the page is visible; it slows/stops when browser lifecycle constraints suspend the page. During reconnect, navigation is disabled and stale data remains visibly marked. On `onreconnected`, the client invokes `GetState`, replaces every presenter field, then re-enables controls. A 401/403 or explicit session-ended message transitions to `expired` and stops retries.

No navigation invocation is queued or retried across a disconnect. One tap creates one invocation. If the acknowledgement is lost, the next state snapshot determines the slide rather than submitting another command.

### 13. Desktop UI boundary

Use a small MVVM boundary with built-in `INotifyPropertyChanged`; do not add a UI framework or design system for the MVP. The main view model projects `PresentationSessionState` and exposes commands on `IPresentationSessionService`. It posts property notifications to the WinUI dispatcher but performs no domain, network, or COM work.

The main grid gives the timer flexible dominant space, then compact PowerPoint and Remote cards. Status always uses icon/text plus color. Overtime uses an explicit label and high-contrast semantic brush. Minimum window size is enforced through `AppWindow`; `OverlappedPresenter.IsAlwaysOnTop` implements the user toggle. Theme follows the Windows setting and uses platform resources for light/dark/high-contrast behavior.

Remote diagnostics distinguish:

- no usable LAN address;
- endpoint bind/listener failure;
- local health success but phone unreachable (same Wi-Fi, address selection, client isolation, VPN, firewall);
- managed-device policy requiring an administrator.

The app may deep-link to the relevant Windows Security page or copy instructions, but it never invokes an elevated firewall mutation.

### 14. Error handling, observability, and retries

Each subsystem exposes stable error codes plus a safe display message; infrastructure exception strings remain local logs. Failures update only the affected state slice. Logs are bounded, local, structured, and contain no speaker-note text, pairing token, browser cookie, or full token-bearing URI.

Retry policy is deliberately narrow:

- PowerPoint attachment/reconciliation: low-frequency continuous retry while the app is open;
- transient COM busy rejection: a small bounded delayed retry, then surface busy;
- server start: bind available endpoints once, then explicit user Retry after failure;
- phone connection: automatic capped backoff while potentially valid;
- Next/Previous: never automatically retry.

### 15. Shutdown ordering

Application shutdown is coordinated once and is idempotent:

1. reject new desktop commands and mark the app closing;
2. invalidate the remote-session credential store;
3. stop hub broadcasts/connections and gracefully stop the web host with a short timeout;
4. stop timer notification scheduling;
5. unsubscribe the application service from adapter events;
6. post PowerPoint cleanup to its STA: unsubscribe COM events, release transient/root RCWs, uninitialize COM, and exit the message loop;
7. release WinUI resources and terminate normally.

The first close request can be deferred/cancelled while asynchronous shutdown completes, then the window closes. Cleanup failures are logged and bounded; the process never kills itself or PowerPoint to force completion. The COM thread is a background thread as a final process-exit safeguard, not as the normal cleanup mechanism.

### 16. Packaging and deployment

For first dogfood, publish `PresentationTimer.App` as an unpackaged, self-contained x64 WinUI 3 application (`WindowsPackageType=None`, .NET self-contained, Windows App SDK self-contained). Include ASP.NET Core, static web assets, interop metadata, and QR dependency in the published output. Do not use NativeAOT because it complicates built-in COM interop and offers little MVP value.

Wrap the tested output in a per-user traditional installer only after the portable publish passes end-to-end checks. The installer adds Start Menu/uninstall entries but does not add firewall rules, require elevation, register a service, or remove user data. Since the MVP stores no durable presentation/session data, rollback is uninstall/delete plus reinstall of the previous build. Code signing is recommended before distribution outside the immediate dogfood group; unsigned SmartScreen behavior is a known internal-testing friction.

MSIX/Store packaging is deferred. It improves identity, servicing, and Store submission but adds signing, capability, inbound-network, and update-path decisions that are unnecessary for the first team trial. ARM64 is deferred; x64 builds must still be manually verified with both 32-bit and 64-bit desktop PowerPoint because PowerPoint is an out-of-process COM server.

## Testing Strategy

### Core unit tests

Use a fake monotonic clock and table-driven state assertions. Cover:

- configure valid/invalid duration;
- start, pause, resume, reset from every timer state;
- crossing zero and pausing/resuming overtime;
- long UI-notification gaps without elapsed-time drift;
- component-slice merge and monotonic revision increments;
- clearing stale presentation fields on disconnect;
- session-token generation shape, uniqueness, hash validation, and end/start invalidation.

### Remote integration tests

Start the real remote host on loopback/dynamic ports with a fake `IPresentationController` behind the real application service. Use the SignalR client and an HTTP cookie container. Cover:

- valid pairing exchange and token-free redirect;
- invalid/missing/expired token denial with no protected payload;
- authenticated SignalR connect and initial full snapshot;
- Next/Previous command routing exactly once;
- broadcast after fake PowerPoint and timer changes;
- transient disconnect, reconnect, and full state replacement;
- ended session closes/denies old connections and cookies;
- new session rejects the prior QR token;
- token/query redaction in captured release logs;
- multiple authenticated connections update desktop connection count.

### PowerPoint integration verification

Do not build a fake COM object graph. Maintain a repeatable manual checklist with a small `.pptx` fixture containing numbered slides, empty and multiline notes, markup-like notes, a hidden slide, and a final slide. Verify on supported Office bitness where available:

1. PowerPoint absent/not registered.
2. PowerPoint installed but not running.
3. PowerPoint running with no presentation.
4. Presentation open without slide show.
5. Application starts first, then slide show starts.
6. Current index, total count, empty notes, multiline notes, and safe markup text.
7. Desktop Next/Previous and phone Next/Previous.
8. Keyboard/clicker slide changes synchronize.
9. Start/end/restart slide show.
10. PowerPoint busy during animation or modal UI.
11. PowerPoint exit during read and later restart/reconnect.
12. Application exit leaves PowerPoint and the presentation open.
13. Repeated attach/detach cycles do not leave orphan PowerPoint processes or steadily grow COM references.

### End-to-end and packaging validation

Run the full Definition of Success from `desktop-presenter-workspace/spec.md` with one iPhone and one Android browser where available. Repeat with Wi-Fi toggled, PC address change, Windows Firewall allowed/blocked, and the published self-contained build on a clean Windows user profile. Every implementation milestone runs solution build and relevant tests; no subsystem is allowed to remain unintegrated until the end.

## Risks / Trade-offs

- **Office COM event and RCW lifetime bugs can leave stale state or PowerPoint references** -> Isolate all COM work on one pumped STA, release scoped RCWs in reverse, unsubscribe first, reconcile periodically, and use the manual repeat-cycle checklist.
- **`SlideShowNextSlide` occurs before transition** -> Treat it as invalidation, defer the read, and never publish a guessed index.
- **PowerPoint can reject calls while busy** -> Use bounded message-pump-friendly retry and preserve a recoverable busy/disconnected state.
- **HTTP bearer credentials can be observed on an untrusted LAN** -> Document the trusted-LAN boundary, use high-entropy short-lived tokens, exchange into HttpOnly cookies, redact logs, and revoke by stopping the session. HTTPS remains future work.
- **Windows Firewall or Wi-Fi client isolation can make a healthy listener unreachable** -> Keep local health separate from remote reachability, show adapter choices and actionable diagnostics, and never silently change system policy.
- **Multiple adapters and IP changes prevent perfect automatic URL choice** -> Expose labeled choices, update QR data, and require rescan when needed instead of claiming seamless migration.
- **In-process web hosting increases app memory and shares process fate** -> Accept for MVP simplicity; keep host lifecycle isolated behind one interface so a later process split is possible if dogfood data justifies it.
- **Self-contained WinUI distribution is larger** -> Accept size in exchange for no runtime prerequisite and predictable dogfood setup.
- **PowerPoint automation cannot be meaningfully covered by normal unit tests** -> Concentrate automation on pure state/remote logic and require a small fixture-driven Office verification matrix before releases.
- **Bundled browser dependencies can become stale** -> Pin the local SignalR client with license/provenance and update it deliberately with server framework upgrades.

## Migration Plan

There is no existing application or user data to migrate.

1. Build and dogfood from an unpackaged Debug/Release output.
2. Produce a self-contained x64 portable publish and run the packaging validation matrix.
3. Optionally wrap that exact tested output in a per-user installer without firewall mutation.
4. Roll back by closing/uninstalling the new build and reinstalling the prior portable/installer version; remote credentials disappear with the process and no database migration is involved.

## References

- [Microsoft PowerPoint SlideShowNextSlide event](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.application.slideshownextslide)
- [Microsoft PowerPoint SlideShowBegin event](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.application.slideshowbegin)
- [Microsoft PowerPoint SlideShowEnd event](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.application.slideshowend)
- [Win32 COM processes, threads, and apartments](https://learn.microsoft.com/en-us/windows/win32/com/processes--threads--and-apartments)
- [Win32 single-threaded apartments](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments)
- [.NET runtime callable wrappers](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/runtime-callable-wrapper)
- [Win32 GetActiveObject](https://learn.microsoft.com/en-us/windows/win32/api/oleauto/nf-oleauto-getactiveobject)
- [ASP.NET Core SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz)
- [ASP.NET Core SignalR security considerations](https://learn.microsoft.com/en-us/aspnet/core/signalr/security)
- [Windows Firewall rules](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules)
- [Unpackaged WinUI 3 distribution](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)
- [PhoneAsPrompter prior art](https://github.com/yangzhongke/PhoneAsPrompter)
- [Zack.ComObjectHelpers prior art](https://github.com/yangzhongke/Zack.ComObjectHelpers)
- [PDF Presentation Tool prior art](https://github.com/DamianoP/PDF_Presentation_Tool)

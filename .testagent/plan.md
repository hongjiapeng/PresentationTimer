# Test Implementation Plan

## Phase 1 — DI composition and lifecycle

- Replace manual object construction with `Microsoft.Extensions.DependencyInjection` registrations.
- Make the application window/page constructors receive their dependencies.
- Centralize ordered, idempotent shutdown and let the service provider own disposal.
- Validate registrations when the provider is built and verify by app build/smoke.

## Phase 2 — Serilog and redaction

- Configure structured JSON file logging below `%LOCALAPPDATA%/PresentationTimer/Logs`.
- Bound files by daily rolling, 5 MiB size, seven-file retention, and seven-day age.
- Bridge Serilog into `Microsoft.Extensions.Logging` and propagate it into Remote and PowerPoint.
- Add captured-event integration coverage proving valid/invalid pairing emits useful events without secrets or speaker notes.

## Phase 3 — shutdown and QR/remote regression

- Add/reuse tests for repeated host shutdown/disposal and authenticated pairing.
- Add QR decode equality if a deterministic decoder can be introduced without a runtime dependency.
- Run scoped tests after each phase, then solution test/build and app smoke.

## Completion mapping

| Checklist item | Planned evidence |
|---|---|
| DI singleton graph | validated provider plus runtime smoke |
| bounded Serilog files | `LogBootstrapper` configuration and build |
| secret-free logs | `Pairing_ValidAndInvalidTokens_LogsOutcomeWithoutCredentials` |
| idempotent lifecycle | `DisposeAsync_WhenRepeated_RemainsStopped` and shutdown coordinator checks |
| regression safety | full solution test/build commands |


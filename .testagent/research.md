# Test Generation Research

## Scope

- Broad verification for the requested Microsoft DI composition, Serilog logging, bounded shutdown, and remote credential redaction.
- SDK-style .NET 10 solution (`PresentationTimer.sln`), `dotnet test` in VSTest mode, MSTest 4.0.1.
- Existing test projects use sealed classes, Arrange/Act/Assert, and project references to Core/PowerPoint or Remote.

## Static pairing baseline

- Polyglot static analyzer: 48 source files, 20 test files, 22 paired, 26 unpaired.
- Relevant paired sources: `RemoteSessionHost.cs`, `PowerPointPresentationController.cs`, and `PresentationSessionService.cs`.
- Relevant unpaired sources: `AppCompositionRoot.cs`, `App.xaml.cs`, and `PairingQrCodeGenerator.cs`.
- This is a parse-only pairing heuristic, not line or branch coverage.

## Target inventory and acceptance checklist

| Requirement | Production targets | Verification target |
|---|---|---|
| IoC uses Microsoft DI and resolves one process-lifetime session graph | App composition and startup | container validation plus structural/runtime smoke evidence |
| Log uses Serilog with bounded local retention | log bootstrap and package manifests | configuration test or deterministic configuration inspection |
| Logs exclude notes, pairing tokens, cookies, and full token-bearing URIs | Remote host pairing/start/stop logs | captured Serilog events around valid and invalid pairing |
| Shutdown is idempotent and continues after individual subsystem failures | `AppCompositionRoot` | focused coordinator tests with fakes where practical |
| Remote host disposal remains repeatable under container ownership | `RemoteSessionHost` | existing repeated stop test plus a repeated-dispose test |

## Commands

- Scoped: `dotnet test tests/PresentationTimer.Remote.Tests/PresentationTimer.Remote.Tests.csproj -c Debug --no-restore`
- Full test: `dotnet test PresentationTimer.sln -c Debug -p:Platform=x64 --no-restore`
- Final build: `dotnet build PresentationTimer.sln -c Release -p:Platform=x64 --no-incremental`


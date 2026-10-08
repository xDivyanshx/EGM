# EGM Core - Project Context

**Last updated:** 2026-10-07
**Status:** Functionally complete, 42 passing tests, 0 warnings, 31.5% line coverage

**Documentation**: Three docs at repo root — `README.md` (user-focused, CLI + web dashboard, 5-step demo), `ARCHITECTURE.md` (developer-focused, design patterns, Mermaid state/sequence diagrams, threading model, testing rationale), and this file (AI session context).

## Overview

**Electronic Gaming Machine (EGM)** - a casino slot machine core control module simulation demonstrating safety-critical software patterns. Built as a learning project to understand regulated gaming systems, state machines, transactional updates, and defensive programming.

## Architecture

### Design Principles
- **Safety-critical mindset** - every state change logged, audit trail, rollback capability
- **Interface-driven** - all services behind interfaces (DI/IoC)
- **Defensive** - Try-Parse patterns, transactional updates with compensation
- **Thread-safe** - one lock per shared resource; `StateManager` routes every read and write through its lock (no `volatile` field)
- **Honest contracts** - methods that can fail report *how* they failed, not just that they returned
- **Separation of concerns** - business logic (core) vs. presentation (CLI/API)

### Key Patterns
1. **Finite State Machine** (`StateManager`) - IDLE/RUNNING/MAINTENANCE/UPDATING/ERROR with strict transition rules
2. **Observer** - `OnStateChanged` event (fired outside lock to avoid deadlock; no-op transitions do not notify)
3. **Try-Parse** - `bool TrySomething(out result, out error)` - never throw for invalid input
4. **Transactional Update with Rollback** - save previous version, attempt install, compensate on failure, return an `UpdateOutcome`
5. **Decorator** - `CapturingLogger` (in `EGM.Api`) wraps `LoggerService` for web UI log capture
6. **Facade** - `LoggerService` simplifies file + console output
7. **Dependency Injection** - Microsoft.Extensions.DependencyInjection, Singleton lifetimes

## Project Structure

```
.  (repo root - the console app project EGM.Core.csproj lives here)
├── Program.cs               # Composition root: DI setup, billValidator.Start(), cli.Run()
├── Services/
│   ├── StateManager.cs      # FSM: transition validation + event notification
│   ├── UpdateManager.cs     # Transactional package install with rollback -> UpdateOutcome
│   ├── ConfigManager.cs     # Read/write config.json (version, timezone, NTP)
│   ├── LoggerService.cs     # Console + file logging with rotation
│   ├── CliProcessor.cs      # REPL: parses commands, delegates to services
│   └── BillValidatorService.cs  # Heartbeat simulation (Timer, ACK timeout → MAINTENANCE)
├── Validators/
│   ├── PackageValidator.cs  # Filename format + version comparison
│   └── TimeZoneValidator.cs # TimeZoneInfo.GetSystemTimeZones() check
├── Persistence/             # Renamed from "Persistance" (was a long-standing typo)
│   └── InstallHistoryStore.cs    # install_history.json append
├── Functions/
│   └── FileFunctions.cs     # LogDirectory (pure) / EnsureLogDirectory (creates), TryReadFile, TryWriteFile
├── Enums/
│   ├── EGMStateEnum.cs      # IDLE/RUNNING/MAINTENANCE/UPDATING/ERROR
│   └── LogTypeEnum.cs       # INFO/WARNING/ERROR/AUDIT
├── Entities/
│   ├── SystemConfig.cs      # CurrentVersion, LastKnownGoodVersion, TimeZone, NtpEnabled
│   ├── InstallRecord.cs     # TimestampUtc, PreviousVersion, InstalledVersion, RolledBack
│   └── UpdateOutcome.cs     # UpdateStatus + Message + versions; the result of an install attempt
├── Interfaces/              # All services behind interfaces for testability
├── Images/                  # Static assets
└── Logs/                    # Data directory (config.json, install_history.json, sample packages)

EGM.Api/                     # Web API + React dashboard (second front-end, same core)
├── Program.cs               # ASP.NET Core: Minimal APIs, static files (.jsx → text/babel)
├── Services/
│   ├── InMemoryLogBuffer.cs # ConcurrentQueue ring buffer (500 lines), monotonic Seq
│   └── CapturingLogger.cs   # Decorator: forwards to LoggerService + buffers for /api/logs
└── wwwroot/
    ├── index.html           # Loads React UMD + Babel standalone
    ├── styles.css           # Dark control-panel theme
    ├── app.jsx              # React (no build step): StatusCard, ControlsCard, UpdateCard, LogPanel
    └── vendor/              # react.production.min.js, react-dom.production.min.js, babel.min.js

EGM.Core.Tests/              # xUnit + Moq, 42 tests, 0 warnings
├── StateManagerTests.cs     # 17 tests: transitions, event firing, no-op suppression, deadlock regression
├── UpdateManagerTests.cs    # 7 tests: success, rollback, validation, state guards
├── PackageValidatorTests.cs # 6 tests: format, version comparison, file existence
├── TimeZoneValidatorTests.cs # 6 tests: valid/invalid/empty timezone IDs
└── FileFunctionsTests.cs    # 6 tests: read/write round-trip, error paths, LogDirectory
```

## Six Core Behaviors (All Verified)

1. **Transactional Update + Rollback**
   `update --package <path>` → validate → pre-install hook (1s delay, fails if "bad" in filename) → commit → history.
   On failure: rollback to the previous version, record rollback, return `UpdateStatus.RolledBack`.

2. **Door-Open Safety Signal**
   `signal door_open` → ForceState(MAINTENANCE) - overrides transition rules, safety-first.

3. **Bill Validator Heartbeat**
   Background Timer (fires immediately, then every 10s): ping → wait 2s for ACK → if no ACK, ForceState(MAINTENANCE).
   `device bill_validator ack on/off` simulates working/broken hardware.

4. **OS Setting Change + Audit**
   `os set-timezone <ZoneId>` → validate against TimeZoneInfo.GetSystemTimeZones() → update config → audit log (old → new).

5. **State Machine (FSM)**
   IDLE ↔ RUNNING, IDLE → UPDATING → IDLE, MAINTENANCE/ERROR can come from anywhere, strict allow-lists elsewhere.
   A **failed update returns to IDLE**, not ERROR.

6. **Logging + Rotation**
   Console (colored) + file (system.log), auto-rotate at 5MB, fallback to system.err on write failure.

## Recent Changes

### Correctness pass (2026-10)
1. **`UpdateOutcome` replaces `void`** - `IUpdateManager.InstallPackage` now returns a record carrying `UpdateStatus` (`Installed`/`Rejected`/`RolledBack`), a human-readable message, and the before/after versions. Previously a rollback was indistinguishable from a success, and `POST /api/update` answered `success: true` unconditionally. `CliProcessor` and the endpoint now report the real outcome.

2. **`StateManager.ForceState` no-op guard** - forcing the state the machine was already in used to log a misleading `MAINTENANCE -> MAINTENANCE` and wake every subscriber. It now mirrors the short-circuit `TransitionTo` always had. Test: `ForceState_WhenAlreadyInTargetState_ShouldNotFireEvent`.

3. **`FileFunctions.LogDirectory` is now pure** - the getter used to call `Directory.CreateDirectory`, so merely *reading* a path wrote to disk. Creation moved to `EnsureLogDirectory()`, called only by the three services that write. `GET /api/packages` also guards a missing directory instead of throwing.

4. **CORS removed from `EGM.Api`** - the dashboard is same-origin and every `fetch` in `app.jsx` is a relative `/api/...` URL, so no cross-origin request is ever made. `AllowAnyOrigin` bought nothing and let any web page the operator visited drive the API.

5. **`app.jsx` shows the real install result** - `UpdateCard` renders the returned status/message instead of blindly calling `setTimeout(refresh, 1400)` and assuming success.

6. **`FileFunctionsTests` de-Windowsified** - the two path tests used `Z:\no_such_drive_egm`. On Linux that is a *legal filename*, so the write would have succeeded, left a junk file, and then poisoned the sibling read test. They now use a path under a guaranteed-absent directory, which fails identically on every platform.

### Earlier fixes
7. **`FileFunctions.LogDirectory`** - replaced hardcoded `..\..\..` with walk-up to `EGM.Core.csproj` marker, so console and API resolve the same root `Logs\` folder regardless of build config.

8. **`Program.cs`** - converted top-level statements → `namespace EGM.Core { class Program { static void Main } }`. User requirement: "I don't want top level in any file."

9. **`CliProcessor.HandleUpdate`** - fixed space-split bug: `parts[2]` → `string.Join(" ", parts.Skip(2))` so paths with spaces survive the command parse.

10. **`StateManager`** - moved `OnStateChanged?.Invoke` outside the lock (was inside `PerformTransition`). Eliminates reentrancy/deadlock risk. Regression test: subscriber calls back into StateManager without hanging.

11. **`PackageValidator`** - resolved CS8625/CS8601 nullable warnings: initialize `newVersion = new Version(0, 0)` placeholder, parse into `Version?`, assign only on success.

### Repo hygiene (2026-10)
- Renamed `Persistance/` → `Persistence/` (namespace was already `EGM.Core.Persistence`, so zero code impact), `Interfaces/Ilogger.cs` → `ILogger.cs`, `Readme.md` → `README.md`, and `Images/successfull_package_update.png` → `successful_package_update.png`.
- Added `LICENSE` (MIT) and `.github/workflows/ci.yml` (build + test on `windows-latest`).
- Added `Images/dashboard.png` — a real headless-Chrome capture of the running dashboard. The README previously had no image of the web UI at all, which is the most visual part of the project.
- Default branch renamed `development` → `main`.
- `.gitignore` now excludes `TestResults/`, `Logs/system.log`, and `Logs/system.err`; those files were untracked. `Logs/config.json` and `Logs/install_history.json` stay tracked on purpose so the documented demo starts from the same version on a fresh clone.

## Technology Stack

- **.NET 8** (EGM.Core, EGM.Api target net8.0; EGM.Core.Tests targets net9.0)
- **Microsoft.Extensions.Hosting** - Generic Host, DI container
- **ASP.NET Core** - Minimal APIs (EGM.Api)
- **React 18** (UMD, no build step) + Babel standalone for in-browser JSX
- **xUnit 2.9.2** + **Moq 4.20.72** - testing
- **coverlet.collector 6.0.2** - code coverage

## Development Setup

### Prerequisites
- .NET SDK 8.x (or newer, with `DOTNET_ROLL_FORWARD=Major`)
- Git

### Build & Run Console
```powershell
cd C:\Projects\EGM
$env:DOTNET_ROLL_FORWARD="Major"
dotnet build EGM.Core.csproj
dotnet run --project EGM.Core.csproj
```

### Build & Run Web Dashboard
```powershell
cd C:\Projects\EGM\EGM.Api
$env:DOTNET_ROLL_FORWARD="Major"
dotnet build
dotnet run
# Open http://localhost:5080
```

### Run Tests
```powershell
cd C:\Projects\EGM
$env:DOTNET_ROLL_FORWARD="Major"
dotnet test EGM.Core.Tests/EGM.Core.Tests.csproj
# With coverage:
dotnet test EGM.Core.Tests/EGM.Core.Tests.csproj --collect:"XPlat Code Coverage"
```

## Test Coverage: 31.5% line (28.1% branch)

### High Coverage (Testable Pure Logic)
- `TimeZoneValidator` - **100%**
- `PackageValidator` - **100%**
- `StateManager` - **96.4%** (gap = the unreachable `default` arm of `IsValidTransition`)
- `UpdateManager` - **91.5%** (gap = the two `UpdateConfig(cfg => ...)` lambda bodies; Moq records the call without invoking the `Action`)
- `FileFunctions` - **85.7%** (gap = the `FindProjectRoot` fallback)
- POCOs `SystemConfig`, `UpdateOutcome` - **100%**

### 0% Coverage (Infrastructure, Not Unit-Testable Without Refactor)
- `LoggerService`, `ConfigManager`, `InstallHistoryStore` - real file I/O on the tracked `Logs\` folder
- `BillValidatorService.PingLoop` - Timer + 2.5s delays per cycle
- `CliProcessor.Run` - infinite loop + `Environment.Exit(0)`
- `Program.Main` - composition root
- `InstallRecord` - POCO, auto-properties, no logic
- `EGM.Api` - not referenced by the test project; endpoint glue over already-tested services

**Decision:** Stop at ~30%. Core business logic validated. Remaining 70% is thin infrastructure requiring invasive abstraction (IFileSystem, ITimer, IEnvironment) for marginal value. See [ARCHITECTURE.md](ARCHITECTURE.md#why-stop-at-315).

## Data Files (git-tracked, in `Logs/`)

- **config.json** - `CurrentVersion: 1.1.8`, `LastKnownGoodVersion: 1.1.7`, `TimeZone: India Standard Time`, `NtpEnabled: true`
- **install_history.json** - 6 records (5 success, 1 rollback)
- **Sample packages** (for testing, in `Logs/`):
  - `update_pkg_1.1.8.txt` (same version as current - rejected, not newer)
  - `update_bad_pkg_1.1.8.txt` (same version + "bad" - rejected)
  - `update_pkg_2.0.0.txt` (valid upgrade - commits)
  - `update_pkg_bad_3.0.0.txt` (newer but "bad" in name - pre-install hook fails → rollback)

**Not tracked:** `system.log` (append-only, grows on every run) and `system.err` — both gitignored.

## CLI Commands

```
start_game              # IDLE → RUNNING
stop_game               # RUNNING → IDLE
signal door_open        # ANY → MAINTENANCE (safety override)
update --package <path> # Transactional install with rollback
device bill_validator ack on/off  # Simulate hardware working/broken
os set-timezone <ZoneId>          # Change timezone + audit
status                  # Print current state + config
exit                    # billValidator.Stop() + Environment.Exit(0)
```

## Web API Endpoints (EGM.Api)

All on http://localhost:5080:

- `GET /api/status` - current state + config
- `GET /api/logs?since=<seq>` - live log stream (polling)
- `GET /api/history` - install history
- `GET /api/packages` - list `*.txt` files in Logs with "pkg" in name
- `GET /api/os/timezones` - all valid TimeZoneInfo IDs
- `POST /api/game/start`, `POST /api/game/stop` - state transitions
- `POST /api/signal/door-open` - safety signal
- `POST /api/device/bill-validator/{ack}` - ack=on/off
- `POST /api/os/timezone` - body: `{"timeZone":"..."}`
- `POST /api/update` - body: `{"packagePath":"update_pkg_2.0.0.txt"}` → returns `{ success, status, message, resolvedPath, previousVersion, installedVersion }`

## Known Issues / Deferred Items

None blocking.

1. **Test untestable infrastructure** - would require wrapping `File`, `Directory`, `Console`, `Timer`, `Environment` behind abstractions (IFileSystem, etc.). Cost/benefit unfavorable for a learning project.

2. **SDK version mismatch** - EGM.Core/EGM.Api target net8.0 and the test project targets net9.0, while this machine has SDK 10.0.x → requires `DOTNET_ROLL_FORWARD=Major` to run the net9.0 test host. Fix: install the .NET 8/9 SDKs, or retarget everything to one framework. CI installs 8.0.x and 9.0.x side by side and needs no roll-forward.

3. **Empty catch in LoggerService fallback** - the final `catch` in `LogWithFallback` writes to console but swallows the exception. Acceptable as last-resort logging when both file and error-file writes fail.

4. **Two unreferenced stale screenshots** - `Images/failed_update.png` and `Images/successful_package_update.png` are no longer referenced by any document. Both show `E:\EGM.Core\Data\`, a path that has never existed in this repository. They are still tracked in git; delete them or re-shoot from this repo.

## Lessons Learned / Teaching Notes

This codebase demonstrates:
- **Why DI matters** - swapped CLI for Web UI with *zero* core changes
- **When to lock** - `StateManager.CurrentState` read, `PerformTransition` mutate, but event *outside* to avoid deadlock
- **Why a locking getter beats `volatile`** - visibility without atomicity is not a memory model
- **Honest return types** - a `void` install method let an API report success after a rollback; the type should carry the outcome
- **Side-effect-free reads** - a property getter that created a directory made every caller a writer
- **Defensive validation** - never throw on bad input; return `(bool success, string error)` tuple
- **Compensating transactions** - save previous state before commit, rollback on exception
- **Observer pitfalls** - firing events while holding locks = reentrancy hazard
- **Path resolution fragility** - hardcoded `..\..\..` breaks across build configs; marker-based walk-up is robust
- **Platform-specific tests** - a `Z:\` path is "invalid" on Windows and a legal filename on Linux
- **Testability trades** - 100% coverage requires wrapping the platform; 30% with high-value tests is often the right stop

## Next Session Quick Start

1. **Explore state machine:** `Services/StateManager.cs` + `StateManagerTests.cs`
2. **Understand update flow:** `Services/UpdateManager.cs` → `RunPreInstallHook` (simulated) → commit or rollback → `UpdateOutcome`
3. **See the two front-ends:** `Program.cs` (CLI) vs. `EGM.Api/Program.cs` (Web) reusing identical core services
4. **Run the dashboard:** `cd EGM.Api && dotnet run`, open http://localhost:5080, install `update_pkg_2.0.0.txt`, watch the log panel + history update
5. **Check tests:** `dotnet test EGM.Core.Tests/EGM.Core.Tests.csproj` - all 42 green, 0 warnings

## References

- Original walkthrough covered 7 topics: Enums, Version, Try-Parse/locks/empty-catch, Singleton/DI/Observer, FSM, heartbeat/volatile, Update/rollback
- React dashboard built without Vite/webpack - vendored React UMD + Babel standalone, `<script type="text/babel">`
- 42 tests: 17 StateManager (including deadlock regression and no-op suppression), 7 UpdateManager, 6 PackageValidator, 6 TimeZoneValidator, 6 FileFunctions

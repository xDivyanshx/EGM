# EGM Core - Project Context

**Last updated:** 2026-08-06  
**Status:** Functionally complete, 40 passing tests, 0 warnings, 29.2% line coverage

## Overview

**Electronic Gaming Machine (EGM)** - a casino slot machine core control module simulation demonstrating safety-critical software patterns. Built as a learning project to understand regulated gaming systems, state machines, transactional updates, and defensive programming.

## Architecture

### Design Principles
- **Safety-critical mindset** - every state change logged, audit trail, rollback capability
- **Interface-driven** - all services behind interfaces (DI/IoC)
- **Defensive** - Try-Parse patterns, transactional updates with compensation
- **Thread-safe** - locks + volatile where needed
- **Separation of concerns** - business logic (core) vs. presentation (CLI/API)

### Key Patterns
1. **Finite State Machine** (`StateManager`) - IDLE/RUNNING/MAINTENANCE/UPDATING/ERROR with strict transition rules
2. **Observer** - `OnStateChanged` event (fired outside lock to avoid deadlock)
3. **Try-Parse** - `bool TrySomething(out result, out error)` - never throw for invalid input
4. **Transactional Update with Rollback** - save previous version, attempt install, compensate on failure
5. **Decorator** - `CapturingLogger` wraps `LoggerService` for web UI log capture
6. **Facade** - `LoggerService` simplifies file + console output
7. **Dependency Injection** - Microsoft.Extensions.DependencyInjection, Singleton lifetimes

## Project Structure

```
EGM.Core/  (repo root - the console app project EGM.Core.csproj lives here)
├── Program.cs               # Composition root: DI setup, billValidator.Start(), cli.Run()
├── Services/
│   ├── StateManager.cs      # FSM: transition validation + event notification
│   ├── UpdateManager.cs     # Transactional package install with rollback
│   ├── ConfigManager.cs     # Read/write config.json (version, timezone, NTP)
│   ├── LoggerService.cs     # Console + file logging with rotation
│   ├── CliProcessor.cs      # REPL: parses commands, delegates to services
│   └── BillValidatorService.cs  # Heartbeat simulation (Timer, ACK timeout → MAINTENANCE)
├── Validators/
│   ├── PackageValidator.cs  # Filename format + version comparison
│   └── TimeZoneValidator.cs # TimeZoneInfo.GetSystemTimeZones() check
├── Persistance/             # NOTE: folder is spelled "Persistance" (misspelling, keep as-is)
│   └── InstallHistoryStore.cs    # install_history.json append
├── Functions/
│   └── FileFunctions.cs     # LogDirectory (walks up to EGM.Core.csproj marker), TryReadFile, TryWriteFile
├── Enums/
│   ├── EGMStateEnum.cs      # IDLE/RUNNING/MAINTENANCE/UPDATING/ERROR
│   └── LogTypeEnum.cs       # INFO/WARNING/ERROR/AUDIT
├── Entities/
│   ├── SystemConfig.cs      # CurrentVersion, LastKnownGoodVersion, TimeZone, NtpEnabled
│   └── InstallRecord.cs     # TimestampUtc, PreviousVersion, InstalledVersion, RolledBack
├── Interfaces/              # All services behind interfaces for testability
├── Images/                  # Static assets
└── Logs/                    # Data directory (config.json, install_history.json, system.log, sample packages)

EGM.Api/                     # Web API + React dashboard (second front-end, same core)
├── Program.cs               # ASP.NET Core: Minimal APIs, CORS, static files (.jsx → text/babel)
├── Services/
│   ├── InMemoryLogBuffer.cs # ConcurrentQueue ring buffer (500 lines), monotonic Seq
│   └── CapturingLogger.cs   # Decorator: forwards to LoggerService + buffers for /api/logs
└── wwwroot/
    ├── index.html           # Loads React UMD + Babel standalone
    ├── styles.css           # Dark control-panel theme
    ├── app.jsx              # React (no build step): StatusCard, ControlsCard, UpdateCard, LogPanel
    └── vendor/              # react.production.min.js, react-dom.production.min.js, babel.min.js

EGM.Core.Tests/              # xUnit + Moq, 40 tests, 0 warnings
├── StateManagerTests.cs     # 15 tests: transitions, event firing, deadlock regression
├── UpdateManagerTests.cs    # 7 tests: success, rollback, validation, state guards
├── PackageValidatorTests.cs # 6 tests: format, version comparison, file existence
├── TimeZoneValidatorTests.cs # 4 tests: valid/invalid/empty timezone IDs
└── FileFunctionsTests.cs    # 8 tests: read/write round-trip, error paths, LogDirectory
```

## Six Core Behaviors (All Verified)

1. **Transactional Update + Rollback**  
   `update --package <path>` → validate → pre-install hook (1s delay, fails if "bad" in filename) → commit → history.  
   On failure: rollback to previous version, record rollback in history.

2. **Door-Open Safety Signal**  
   `signal door_open` → ForceState(MAINTENANCE) - overrides transition rules, safety-first.

3. **Bill Validator Heartbeat**  
   Background Timer (10s interval): ping → wait 2s for ACK → if no ACK, ForceState(MAINTENANCE).  
   `device bill_validator ack on/off` simulates working/broken hardware.

4. **OS Setting Change + Audit**  
   `os set-timezone <ZoneId>` → validate against TimeZoneInfo.GetSystemTimeZones() → update config → audit log (old → new).

5. **State Machine (FSM)**  
   IDLE ↔ RUNNING, IDLE → UPDATING → IDLE, MAINTENANCE/ERROR can come from anywhere, strict allow-lists elsewhere.

6. **Logging + Rotation**  
   Console (colored) + file (system.log), auto-rotate at 5MB, fallback to system.err on write failure.

## Recent Changes (Bug Fixes Applied)

All five backlog items resolved, verified by tests:

1. **`FileFunctions.LogDirectory`** - replaced hardcoded `..\..\..` with walk-up to `EGM.Core.csproj` marker. Now both console and API resolve the same root `Logs\` folder regardless of build config. (87% coverage)

2. **`Program.cs`** - converted top-level statements → `namespace EGM.Core { class Program { static void Main } }`. User requirement: "I don't want top level in any file."

3. **`CliProcessor.HandleUpdate`** - fixed space-split bug: `parts[2]` → `string.Join(" ", parts.Skip(2))` so paths with spaces survive the command parse.

4. **`StateManager`** - moved `OnStateChanged?.Invoke` outside the lock (was inside `PerformTransition`). Eliminates reentrancy/deadlock risk. Regression test added: subscriber calls back into StateManager without hanging.

5. **`PackageValidator`** - resolved CS8625/CS8601 nullable warnings: initialize `newVersion = new Version(0, 0)` placeholder, parse into `Version?`, assign only on success. (100% coverage)

## Technology Stack

- **.NET 8** (targets net8.0, runs on SDK 10.0.302 with `DOTNET_ROLL_FORWARD=Major`)
- **Microsoft.Extensions.Hosting** - Generic Host, DI container
- **ASP.NET Core** - Minimal APIs (EGM.Api)
- **React 18** (UMD, no build step) + Babel standalone for in-browser JSX
- **xUnit 2.9.2** + **Moq 4.20.72** - testing
- **coverlet.collector 6.0.2** - code coverage

## Development Setup

### Prerequisites
- .NET SDK 10.x (or 8.x/9.x)
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
cd C:\Projects\EGM\EGM.Core.Tests
$env:DOTNET_ROLL_FORWARD="Major"
dotnet test
# With coverage:
dotnet test --collect:"XPlat Code Coverage"
```

## Test Coverage: 29.2% (172/589 lines)

### High Coverage (Testable Pure Logic)
- `TimeZoneValidator` - **100%**
- `PackageValidator` - **100%**
- `StateManager` - **96%** (4% = unreachable default branch)
- `UpdateManager` - **92%**
- `FileFunctions` - **87%**

### 0% Coverage (Infrastructure, Not Unit-Testable Without Refactor)
- `LoggerService`, `ConfigManager`, `InstallHistoryStore` - call static `FileFunctions.LogDirectory`, would mutate real git-tracked `Logs\`
- `BillValidatorService.PingLoop` - Timer + 2.5s delays per cycle
- `CliProcessor.Run` - infinite loop + `Environment.Exit(0)`
- `Program.Main` - composition root
- POCOs (`SystemConfig`, `InstallRecord`) - auto-properties, no logic

**Decision:** Stop at ~30%. Core business logic validated. Remaining 70% is thin infrastructure requiring invasive abstraction (IFileSystem, ITimer, IEnvironment) for marginal value.

## Data Files (git-tracked, in `Logs/`)

- **config.json** - `CurrentVersion: 1.1.8`, `LastKnownGoodVersion: 1.1.7`, `TimeZone: India Standard Time`, `NtpEnabled: true`
- **install_history.json** - 6 records (5 success, 1 rollback)
- **system.log** - runtime log (grows, rotate at 5MB)
- **Sample packages** (for testing, in `Logs/`):
  - `update_pkg_1.1.8.txt` (same version as current - rejected, not newer)
  - `update_bad_pkg_1.1.8.txt` (same version + "bad" - rejected)
  - `update_pkg_2.0.0.txt` (valid upgrade - commits)
  - `update_pkg_bad_3.0.0.txt` (newer but "bad" in name - pre-install hook fails → rollback)

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
- `POST /api/update` - body: `{"packagePath":"update_pkg_2.0.0.txt"}`

## Known Issues / Deferred Items

None blocking. The following are *not* bugs, just areas for future enhancement if this becomes a library for others:

1. **Test untestable infrastructure** - would require wrapping `File`, `Directory`, `Console`, `Timer`, `Environment` behind abstractions (IFileSystem, etc.). Cost/benefit unfavorable for a learning project.

2. **SDK version mismatch** - project targets net8.0, machine has SDK 10.0.302 → requires `DOTNET_ROLL_FORWARD=Major` to run. Fix: install .NET 8 SDK, or retarget to net9.0/net10.0.

3. **Empty catch in LoggerService fallback** - the final `catch` in `LogWithFallback` writes to console but swallows the exception. Acceptable as last-resort logging when both file and error-file writes fail.

4. **`Logs/system.log` grows unbounded in git** - runtime log lines append on every run. Suggestion: add `Logs/*.log` to `.gitignore` (keep the .json files tracked).

## Lessons Learned / Teaching Notes

This codebase demonstrates:
- **Why DI matters** - swapped CLI for Web UI with *zero* core changes
- **When to lock** - `StateManager.CurrentState` read, `PerformTransition` mutate, but event *outside* to avoid deadlock
- **Defensive validation** - never throw on bad input; return `(bool success, string error)` tuple
- **Compensating transactions** - save previous state before commit, rollback on exception
- **Observer pitfalls** - firing events while holding locks = reentrancy hazard
- **Path resolution fragility** - hardcoded `..\..\..` breaks across build configs; marker-based walk-up is robust
- **Testability trades** - 100% coverage requires wrapping the platform; 30% with high-value tests is often the right stop

## Next Session Quick Start

1. **Explore state machine:** `Services/StateManager.cs` + `StateManagerTests.cs`
2. **Understand update flow:** `Services/UpdateManager.cs` → `RunPreInstallHook` (simulated) → commit or rollback
3. **See the two front-ends:** `Program.cs` (CLI) vs. `EGM.Api/Program.cs` (Web) reusing identical core services
4. **Run the dashboard:** `cd EGM.Api && dotnet run`, open http://localhost:5080, install `update_pkg_2.0.0.txt`, watch the log panel + history update
5. **Check tests:** `cd EGM.Core.Tests && dotnet test` - all 40 green, 0 warnings

## References

- Original walkthrough covered 7 topics: Enums, Version, Try-Parse/locks/empty-catch, Singleton/DI/Observer, FSM, heartbeat/volatile, Update/rollback
- React dashboard built without Vite/webpack - vendored React UMD + Babel standalone, `<script type="text/babel">`
- Five bug fixes applied: path resolution, top-level→class, space-split, event-outside-lock, nullable warnings
- 40 tests added: 15 StateManager (including deadlock regression), 7 UpdateManager, 6 PackageValidator, 4 TimeZoneValidator, 8 FileFunctions

using EGM.Api.Services;
using EGM.Core.Entities;
using EGM.Core.Enums;
using EGM.Core.Interfaces;
using EGM.Core.Persistence;
using EGM.Core.Services;
using EGM.Core.Validators;

// Disambiguate: both EGM.Core.Interfaces and Microsoft.Extensions.Logging define ILogger.
using ILogger = EGM.Core.Interfaces.ILogger;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Dependency Injection - SAME service graph as the console app's Program.cs.
// The only differences:
//   * ILogger is wrapped in a CapturingLogger so the web UI can read log lines.
//   * We do NOT register/run CliProcessor - the React frontend is the shell instead.
// Every other service (StateManager, UpdateManager, BillValidator, ...) is reused
// exactly as-is from EGM.Core.
// ---------------------------------------------------------------------------
var services = builder.Services;

// Web-only: the in-memory log buffer the UI polls.
services.AddSingleton<InMemoryLogBuffer>();

// ILogger = real LoggerService, decorated to also capture into the buffer.
services.AddSingleton<ILogger>(sp =>
{
    var real = new LoggerService();
    var buffer = sp.GetRequiredService<InMemoryLogBuffer>();
    return new CapturingLogger(real, buffer);
});

services.AddSingleton<IConfigManager, ConfigManager>();
services.AddSingleton<IInstallHistoryStore, InstallHistoryStore>();
services.AddSingleton<IStateManager, StateManager>();
services.AddSingleton<IBillValidator, BillValidatorService>();
services.AddSingleton<IPackageValidator, PackageValidator>();
services.AddSingleton<ITimeZoneValidator, TimeZoneValidator>();
services.AddSingleton<IUpdateManager, UpdateManager>();
// NOTE: ICliProcessor is intentionally not registered here (see EGM.Core/Services/CliProcessor.cs).

services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup: start the background heartbeat, same as the console app did.
// ---------------------------------------------------------------------------
var billValidator = app.Services.GetRequiredService<IBillValidator>();
billValidator.Start();

app.UseCors();
app.UseDefaultFiles();   // serve wwwroot/index.html at "/"

// Teach the static-file middleware about .jsx (unknown by default => would 404).
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".jsx"] = "text/babel";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

// ===========================================================================
// REST endpoints - thin wrappers over the existing services. No business logic
// lives here; each endpoint just translates HTTP -> a service method call,
// exactly like CliProcessor translated a typed command -> a service call.
// ===========================================================================

// --- Status / dashboard ---------------------------------------------------
app.MapGet("/api/status", (IConfigManager cfg, IStateManager state) =>
{
    var c = cfg.GetConfig();
    return Results.Ok(new
    {
        state = state.CurrentState.ToString(),
        currentVersion = c.CurrentVersion.ToString(),
        lastKnownGoodVersion = c.LastKnownGoodVersion.ToString(),
        timeZone = c.TimeZone,
        ntpEnabled = c.NtpEnabled
    });
});

// --- Live logs (UI polls with ?since=<lastSeq>) ---------------------------
app.MapGet("/api/logs", (InMemoryLogBuffer buffer, long? since) =>
{
    var lines = buffer.GetSince(since ?? 0);
    return Results.Ok(lines.Select(l => new
    {
        seq = l.Seq,
        timestamp = l.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
        level = l.Level,
        message = l.Message
    }));
});

// --- State machine (Behavior #5) ------------------------------------------
app.MapPost("/api/game/start", (IStateManager state) =>
{
    bool ok = state.TransitionTo(EGMStateEnum.RUNNING, "Operator started game (web)");
    return Results.Ok(new { success = ok, state = state.CurrentState.ToString() });
});

app.MapPost("/api/game/stop", (IStateManager state) =>
{
    bool ok = state.TransitionTo(EGMStateEnum.IDLE, "Operator stopped game (web)");
    return Results.Ok(new { success = ok, state = state.CurrentState.ToString() });
});

// --- Door-open safety signal (Behavior #2) --------------------------------
app.MapPost("/api/signal/door-open", (IStateManager state) =>
{
    state.ForceState(EGMStateEnum.MAINTENANCE, "Door opened (web)");
    return Results.Ok(new { success = true, state = state.CurrentState.ToString() });
});

// --- Bill validator simulation (Behavior #3) ------------------------------
// on  => working hardware;  off => broken (no ACKs) => heartbeat forces MAINTENANCE.
app.MapPost("/api/device/bill-validator/{ack}", (string ack, IBillValidator bill) =>
{
    bool shouldFail = ack.Equals("off", StringComparison.OrdinalIgnoreCase);
    bill.SetSimulatedFailure(shouldFail);
    return Results.Ok(new { success = true, simulatedFailure = shouldFail });
});

// --- OS setting change + audit (Behavior #4) ------------------------------
app.MapPost("/api/os/timezone", (SetTimezoneRequest req, ITimeZoneValidator tzv,
                                 IConfigManager cfg, ILogger logger) =>
{
    if (!tzv.ValidateTimeZone(req.TimeZone, out string error))
        return Results.BadRequest(new { success = false, error });

    var oldZone = cfg.GetConfig().TimeZone;
    cfg.UpdateConfig(c => c.TimeZone = req.TimeZone);
    logger.Audit("Operator", "Set Timezone", oldZone, req.TimeZone);
    return Results.Ok(new { success = true, timeZone = req.TimeZone });
});

// List of valid timezone IDs so the UI can offer a dropdown.
app.MapGet("/api/os/timezones", () =>
    Results.Ok(TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(id => id)));

// --- Transactional update + rollback (Behavior #1) ------------------------
// The UI sends a package filename; we resolve it against the Logs folder (where the
// sample packages live) unless an absolute path is given. This mirrors the CLI's
// `update --package <path>`.
app.MapPost("/api/update", (UpdateRequest req, IUpdateManager update) =>
{
    if (string.IsNullOrWhiteSpace(req.PackagePath))
        return Results.BadRequest(new { success = false, error = "packagePath is required" });

    string path = req.PackagePath;
    if (!Path.IsPathRooted(path))
        path = Path.Combine(EGM.Core.Infrastructure.FileFunctions.LogDirectory, path);

    update.InstallPackage(path);
    // InstallPackage is void and self-logging; the UI reads the outcome from /api/logs
    // and the refreshed /api/status. We just acknowledge receipt here.
    return Results.Ok(new { success = true, resolvedPath = path });
});

// --- Install history (reads install_history.json) -------------------------
app.MapGet("/api/history", (IInstallHistoryStore history) =>
{
    var records = history.GetHistory()
        .OrderByDescending(r => r.TimestampUtc)
        .Select(r => new
        {
            timestamp = r.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            previousVersion = r.PreviousVersion.ToString(),
            installedVersion = r.InstalledVersion.ToString(),
            rolledBack = r.RolledBack
        });
    return Results.Ok(records);
});

// List the sample package files available in the Logs folder, for convenience.
app.MapGet("/api/packages", () =>
{
    var dir = EGM.Core.Infrastructure.FileFunctions.LogDirectory;
    var files = Directory.GetFiles(dir, "*.txt")
        .Select(Path.GetFileName)
        .Where(n => n!.Contains("pkg", StringComparison.OrdinalIgnoreCase))
        .OrderBy(n => n);
    return Results.Ok(files);
});

app.Run();

// Request DTOs
record SetTimezoneRequest(string TimeZone);
record UpdateRequest(string PackagePath);

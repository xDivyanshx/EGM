const { useState, useEffect, useCallback, useRef } = React;

// ---------------------------------------------------------------------------
// Tiny API helper - every call maps to one REST endpoint on EGM.Api.
// ---------------------------------------------------------------------------
const api = {
    get: (url) => fetch(url).then((r) => r.json()),
    post: (url, body) =>
        fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: body ? JSON.stringify(body) : undefined,
        }).then(async (r) => ({ ok: r.ok, data: await r.json().catch(() => ({})) })),
};

// ---------------------------------------------------------------------------
// Status card - live view of the machine (Behavior #5).
// ---------------------------------------------------------------------------
function StatusCard({ status }) {
    if (!status) return null;
    return (
        <div className="card">
            <h2>Machine Status</h2>
            <div className={"status-state state-" + status.state}>{status.state}</div>
            <div className="kv"><span className="k">Current Version</span><span className="v">{status.currentVersion}</span></div>
            <div className="kv"><span className="k">Last Known Good</span><span className="v">{status.lastKnownGoodVersion}</span></div>
            <div className="kv"><span className="k">Timezone</span><span className="v">{status.timeZone}</span></div>
            <div className="kv"><span className="k">NTP Enabled</span><span className="v">{String(status.ntpEnabled)}</span></div>
        </div>
    );
}

// ---------------------------------------------------------------------------
// Game controls + door signal (Behaviors #5 and #2).
// ---------------------------------------------------------------------------
function ControlsCard({ onAction }) {
    return (
        <div className="card">
            <h2>State Controls</h2>
            <div className="btn-row">
                <button className="btn btn-green" onClick={() => onAction("/api/game/start")}>▶ Start Game</button>
                <button className="btn" onClick={() => onAction("/api/game/stop")}>■ Stop Game</button>
                <button className="btn btn-red" onClick={() => onAction("/api/signal/door-open")}>⚠ Signal Door Open</button>
            </div>
            <p className="hint">
                Start moves IDLE → RUNNING. Stop returns to IDLE. Door Open force-transitions to
                MAINTENANCE from any state (safety override).
            </p>
        </div>
    );
}

// ---------------------------------------------------------------------------
// Bill validator hardware simulation (Behavior #3).
// ---------------------------------------------------------------------------
function BillValidatorCard({ onAction }) {
    const [broken, setBroken] = useState(false);

    const setState = async (ack) => {
        await onAction("/api/device/bill-validator/" + ack);
        setBroken(ack === "off");
    };

    return (
        <div className="card">
            <h2>Bill Validator (Hardware)</h2>
            <div style={{ marginBottom: "12px" }}>
                Status:{" "}
                {broken
                    ? <span className="pill pill-bad">BROKEN — no ACKs</span>
                    : <span className="pill pill-ok">WORKING</span>}
            </div>
            <div className="btn-row">
                <button className="btn btn-green" onClick={() => setState("on")}>Set Working (ack on)</button>
                <button className="btn btn-red" onClick={() => setState("off")}>Simulate Failure (ack off)</button>
            </div>
            <p className="hint">
                The heartbeat pings every 10s. If you simulate failure, the next ping gets no ACK
                and the machine is forced into MAINTENANCE — watch the log &amp; status update on their own.
            </p>
        </div>
    );
}

// ---------------------------------------------------------------------------
// OS setting: timezone change + audit (Behavior #4).
// ---------------------------------------------------------------------------
function TimezoneCard({ status, onAction, refresh }) {
    const [zones, setZones] = useState([]);
    const [selected, setSelected] = useState("");

    useEffect(() => {
        api.get("/api/os/timezones").then(setZones);
    }, []);

    useEffect(() => {
        if (status && !selected) setSelected(status.timeZone);
    }, [status]);

    const apply = async () => {
        const res = await onAction("/api/os/timezone", { timeZone: selected });
        if (res && res.ok === false) alert("Timezone rejected: " + (res.data.error || "invalid"));
        refresh();
    };

    return (
        <div className="card">
            <h2>OS Setting — Timezone</h2>
            <div className="field">
                <label>Timezone ID</label>
                <select value={selected} onChange={(e) => setSelected(e.target.value)}>
                    {zones.map((z) => <option key={z} value={z}>{z}</option>)}
                </select>
            </div>
            <button className="btn" onClick={apply}>Apply &amp; Audit</button>
            <p className="hint">Writes to config.json and records an [AUDIT] log entry (old → new).</p>
        </div>
    );
}

// ---------------------------------------------------------------------------
// Transactional update + rollback (Behavior #1).
// ---------------------------------------------------------------------------
function UpdateCard({ onAction, refresh }) {
    const [packages, setPackages] = useState([]);
    const [pkg, setPkg] = useState("");

    const loadPackages = useCallback(() => {
        api.get("/api/packages").then((list) => {
            setPackages(list);
            if (list.length && !pkg) setPkg(list[0]);
        });
    }, [pkg]);

    useEffect(() => { loadPackages(); }, []);

    const install = async () => {
        await onAction("/api/update", { packagePath: pkg });
        // Give the pre-install hook (1s simulated) time, then refresh status + history.
        setTimeout(refresh, 1400);
    };

    return (
        <div className="card">
            <h2>Package Update</h2>
            <div className="field">
                <label>Package (from Logs folder)</label>
                <select value={pkg} onChange={(e) => setPkg(e.target.value)}>
                    {packages.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
            </div>
            <div className="btn-row">
                <button className="btn btn-amber" onClick={install}>⬆ Install Update</button>
                <button className="btn" onClick={loadPackages}>↻ Refresh list</button>
            </div>
            <p className="hint">
                Validates (exists, format, newer version), runs a pre-install hook, then commits —
                or rolls back automatically if the filename contains "bad". Machine must be IDLE.
            </p>
        </div>
    );
}

// ---------------------------------------------------------------------------
// Install history table - all installs/rollbacks from install_history.json.
// ---------------------------------------------------------------------------
function HistoryCard({ history }) {
    return (
        <div className="card full">
            <h2>Install History</h2>
            {history.length === 0 ? (
                <p className="hint">No install records yet.</p>
            ) : (
                <table>
                    <thead>
                        <tr>
                            <th>Timestamp</th>
                            <th>Previous</th>
                            <th>Installed</th>
                            <th>Result</th>
                        </tr>
                    </thead>
                    <tbody>
                        {history.map((rec, i) => (
                            <tr key={i}>
                                <td className="mono">{rec.timestamp}</td>
                                <td className="mono">{rec.previousVersion}</td>
                                <td className="mono">{rec.installedVersion}</td>
                                <td>
                                    {rec.rolledBack ? (
                                        <span className="badge badge-rollback">ROLLED BACK</span>
                                    ) : (
                                        <span className="badge badge-ok">SUCCESS</span>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}

// ---------------------------------------------------------------------------
// Live log stream - polls the in-memory buffer captured from the real logger.
// ---------------------------------------------------------------------------
function LogPanel({ logs }) {
    const ref = useRef(null);
    useEffect(() => {
        if (ref.current) ref.current.scrollTop = ref.current.scrollHeight;
    }, [logs]);

    return (
        <div className="card full">
            <h2>Live System Log</h2>
            <div className="log-panel" ref={ref}>
                {logs.map((l, i) => (
                    <div key={i} className={"log-line log-" + l.level}>
                        <span className="log-time">{l.timestamp}</span>
                        [{l.level}] {l.message}
                    </div>
                ))}
            </div>
        </div>
    );
}

// ---------------------------------------------------------------------------
// Root component - owns polled state and wires everything together.
// ---------------------------------------------------------------------------
function App() {
    const [status, setStatus] = useState(null);
    const [logs, setLogs] = useState([]);
    const [history, setHistory] = useState([]);
    const [toast, setToast] = useState(null);
    const prevState = useRef(null);

    const refresh = useCallback(() => {
        api.get("/api/status").then(setStatus);
        api.get("/api/logs").then(setLogs);
        api.get("/api/history").then(setHistory);
    }, []);

    // Initial load + poll every 2s so background events (heartbeat) show up on their own.
    useEffect(() => {
        refresh();
        const id = setInterval(refresh, 2000);
        return () => clearInterval(id);
    }, [refresh]);

    // Toast when the state changes (mirrors the console's OnStateChanged handler).
    useEffect(() => {
        if (!status) return;
        if (prevState.current && prevState.current !== status.state) {
            if (status.state === "MAINTENANCE") setToast({ cls: "toast-red", msg: "⚠ SYSTEM ENTERED MAINTENANCE MODE!" });
            else if (status.state === "RUNNING") setToast({ cls: "toast-green", msg: "▶ Game Started!" });
            if (status.state === "MAINTENANCE" || status.state === "RUNNING") {
                setTimeout(() => setToast(null), 3500);
            }
        }
        prevState.current = status.state;
    }, [status]);

    // Fire an action endpoint, then refresh. Returns the response for callers that care.
    const onAction = useCallback(async (url, body) => {
        const res = await api.post(url, body);
        refresh();
        return res;
    }, [refresh]);

    return (
        <div className="app">
            {toast && <div className={"toast " + toast.cls}>{toast.msg}</div>}
            <header className="topbar">
                <div>
                    <h1>EGM CORE — CONTROL PANEL</h1>
                    <div className="sub">Electronic Gaming Machine · core control module simulation</div>
                </div>
                <div className="sub">auto-refresh 2s</div>
            </header>

            <div className="grid">
                <StatusCard status={status} />
                <ControlsCard onAction={onAction} />
                <BillValidatorCard onAction={onAction} />
                <TimezoneCard status={status} onAction={onAction} refresh={refresh} />
                <UpdateCard onAction={onAction} refresh={refresh} />
                <HistoryCard history={history} />
                <LogPanel logs={logs} />
            </div>
        </div>
    );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);

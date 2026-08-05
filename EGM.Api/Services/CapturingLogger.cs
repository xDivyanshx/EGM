using EGM.Core.Enums;
using EGM.Core.Interfaces;

// Disambiguate: both EGM.Core.Interfaces and Microsoft.Extensions.Logging define ILogger.
using ILogger = EGM.Core.Interfaces.ILogger;

namespace EGM.Api.Services
{
    /// <summary>
    /// Decorator over the real ILogger (LoggerService). It forwards every call to the
    /// inner logger (so console + file logging still happen exactly as before) and ALSO
    /// records the entry in the in-memory buffer for the web UI to display.
    ///
    /// This is the Decorator pattern: same ILogger contract, added behaviour, zero
    /// changes to any EGM.Core class.
    /// </summary>
    public class CapturingLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly InMemoryLogBuffer _buffer;

        public CapturingLogger(ILogger inner, InMemoryLogBuffer buffer)
        {
            _inner = inner;
            _buffer = buffer;
        }

        public void Log(LogTypeEnum type, string message)
        {
            _inner.Log(type, message);
            _buffer.Add(type.ToString().ToUpper(), message);
        }

        public void Audit(string actor, string action, string oldValue, string newValue)
        {
            _inner.Audit(actor, action, oldValue, newValue);
            _buffer.Add("AUDIT", $"User: {actor} | Action: {action} | Old: '{oldValue}' -> New: '{newValue}'");
        }
    }
}

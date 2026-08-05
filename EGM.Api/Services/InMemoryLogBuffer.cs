using System.Collections.Concurrent;

namespace EGM.Api.Services
{
    /// <summary>
    /// A thread-safe ring buffer holding the most recent log lines so the web UI can
    /// poll and display them. Lives in the API layer only - the core is untouched.
    /// </summary>
    public class InMemoryLogBuffer
    {
        private readonly ConcurrentQueue<LogLine> _lines = new();
        private const int MaxLines = 500;
        private long _seq;

        public void Add(string level, string message)
        {
            var line = new LogLine
            {
                Seq = Interlocked.Increment(ref _seq),
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                Message = message
            };
            _lines.Enqueue(line);

            // Trim to the cap.
            while (_lines.Count > MaxLines && _lines.TryDequeue(out _)) { }
        }

        /// <summary>Return log lines with Seq greater than <paramref name="afterSeq"/>.</summary>
        public IReadOnlyList<LogLine> GetSince(long afterSeq) =>
            _lines.Where(l => l.Seq > afterSeq).OrderBy(l => l.Seq).ToList();

        public class LogLine
        {
            public long Seq { get; set; }
            public DateTime TimestampUtc { get; set; }
            public string Level { get; set; } = "";
            public string Message { get; set; } = "";
        }
    }
}

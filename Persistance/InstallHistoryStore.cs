using EGM.Core.Entities;
using EGM.Core.Enums;
using EGM.Core.Infrastructure;
using EGM.Core.Interfaces;
using System.Text.Json;

namespace EGM.Core.Persistence
{
    public class InstallHistoryStore : IInstallHistoryStore
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private readonly ILogger _logger;
        private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        public InstallHistoryStore(ILogger logger)
        {
            string dataDir = FileFunctions.LogDirectory;
            _filePath = Path.Combine(dataDir, "install_history.json");
            if (!File.Exists(_filePath))
                FileFunctions.TryWriteFile(_filePath, "[]", out _);
            _logger = logger;
        }

        public void RecordInstall(Version previousVersion, Version newVersion)
        {
            var record = new InstallRecord {TimestampUtc = DateTime.UtcNow, PreviousVersion = previousVersion, InstalledVersion = newVersion,RolledBack = false};

            AppendRecord(record);
        }

        public void RecordRollback(Version targetVersion)
        {
            var record = new InstallRecord { TimestampUtc = DateTime.UtcNow, PreviousVersion = targetVersion, InstalledVersion = targetVersion, RolledBack = true};

            AppendRecord(record);
        }

        public IReadOnlyCollection<InstallRecord> GetHistory()
        {
            lock (_lock)
            {
                if (!FileFunctions.TryReadFile(_filePath, out var json, out var error))
                {
                    _logger.Log(LogTypeEnum.Error,$"Could not read install history: {error}");
                    return Array.Empty<InstallRecord>();
                }

                try
                {
                    var records = JsonSerializer.Deserialize<List<InstallRecord>>(json) ?? new List<InstallRecord>(); ;
                    return records ;
                }
                catch (JsonException ex)
                {
                    _logger.Log(LogTypeEnum.Error, $"Install history file is corrupted.: {ex.Message}");
                    return [];
                }
            }
        }


        private void AppendRecord(InstallRecord record)
        {
            lock (_lock)
            {
                // Read existing history
                if (!FileFunctions.TryReadFile(_filePath, out var json, out _))
                {
                    json = "[]";
                }

                var history = JsonSerializer .Deserialize<List<InstallRecord>>(json) ?? new List<InstallRecord>();

                history.Add(record);

                var updatedJson = JsonSerializer.Serialize(history, _options);

                if (!FileFunctions.TryWriteFile(_filePath, updatedJson, out var error))
                {
                    _logger.Log(LogTypeEnum.Error,$"Failed to write install history: {error}");
                }
            }
        }

    }
}

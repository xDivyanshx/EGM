using EGM.Core.Entities;
using EGM.Core.Enums;
using EGM.Core.Infrastructure;
using EGM.Core.Interfaces;
using System;
using System.IO;
using System.Text.Json;

namespace EGM.Core.Services
{
    public class ConfigManager : IConfigManager
    {
        private readonly string _configPath;
        private readonly ILogger _logger;
        private SystemConfig _config = new();
        private readonly object _lock = new();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };


        public ConfigManager(ILogger logger)
        {
            _logger = logger;
            string dataDir = FileFunctions.LogDirectory;
            _configPath = Path.Combine(dataDir, "config.json");
            LoadConfig();
        }

        public SystemConfig GetConfig()
        {
            lock (_lock)
                return new SystemConfig
                {
                    CurrentVersion = _config.CurrentVersion,
                    LastKnownGoodVersion = _config.LastKnownGoodVersion,
                    TimeZone = _config.TimeZone,
                    NtpEnabled = _config.NtpEnabled
                };
        }

        public void UpdateConfig(Action<SystemConfig> updateAction)
        {
            lock (_lock)
            {
                updateAction(_config);
                SaveConfig();
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<SystemConfig>(json) ?? new SystemConfig();
                    _logger.Log(LogTypeEnum.Info, "Configuration loaded.");
                }
                catch (Exception ex)
                {
                    _logger.Log(LogTypeEnum.Error, $"Config load failed: {ex.Message}");
                    _config = new SystemConfig();
                }
            }
            else
            {
                _config = new SystemConfig();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            string json = JsonSerializer.Serialize(_config, _jsonOptions);

            if(FileFunctions.TryWriteFile(_configPath, json, out string errorMessage))
                _logger.Log(LogTypeEnum.Info, "Configuration saved.");
            else
                _logger.Log(LogTypeEnum.Error, $"Config save failed: {errorMessage}");
        }
    }
}
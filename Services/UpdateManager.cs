using EGM.Core.Interfaces;
using EGM.Core.Enums;
using System.Diagnostics.Eventing.Reader;

namespace EGM.Core.Services
{
    public class UpdateManager : IUpdateManager
    {
        private readonly IConfigManager _config;
        private readonly IStateManager _state;
        private readonly ILogger _logger;
        private readonly IPackageValidator _packageValidator;
        private readonly IInstallHistoryStore _history;

        public UpdateManager( IConfigManager config, IStateManager state,ILogger logger,IPackageValidator validator, IInstallHistoryStore history)
        {
            _config = config;
            _state = state;
            _logger = logger;
            _packageValidator = validator;
            _history = history;
        }

        public void InstallPackage(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                _logger.Log(LogTypeEnum.Warning, "[Update] Package path cannot be empty.");
                return;
            }

            _logger.Log(LogTypeEnum.Info, $"[Update] Starting installation for: {packagePath}");

            // Ensure system is in IDLE before updating
            if (!_state.TransitionTo(EGMStateEnum.UPDATING, "User initiated update"))
            {
                _logger.Log(LogTypeEnum.Warning, "[Update] System must be in IDLE state to install updates.");
                return;
            }

            var currentConfig = _config.GetConfig();
            Version previousVersion = currentConfig.CurrentVersion;

            
            if (!_packageValidator.TryValidateAndExtractVersion( packagePath, previousVersion, out Version newVersion,  out string errorMessage))
            {
                _logger.Log(LogTypeEnum.Error, $"[Update] Validation failed: {errorMessage}");
                _state.TransitionTo(EGMStateEnum.IDLE, "Update validation failed");
                return; 
            }

            try
            {
                _logger.Log(LogTypeEnum.Info, $"[Update] Package validated. Target version: {newVersion}");

                RunPreInstallHook(packagePath);

                // Commit update
                _config.UpdateConfig(cfg =>
                {
                    cfg.LastKnownGoodVersion = previousVersion;
                    cfg.CurrentVersion = newVersion;
                });

                // Record success
                _history.RecordInstall(previousVersion, newVersion);

                _logger.Log(LogTypeEnum.Info, $"[Update] Installation successful. Active version: {newVersion}");

                _state.TransitionTo(EGMStateEnum.IDLE, "Update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.Log(LogTypeEnum.Error, $"[Update] Installation failed: {ex.Message}");

                PerformRollback(previousVersion);

                _state.TransitionTo(EGMStateEnum.IDLE, "Rollback completed");
            }
        }



        private void RunPreInstallHook(string packagePath)
        {
            _logger.Log(LogTypeEnum.Info, "[Update] Running pre-install script...");

            // Simulated execution delay
            Thread.Sleep(1000);

            if (packagePath.Contains("bad",StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Pre-install script failed.");
            }
        }

        private void PerformRollback(Version previousVersion)
        {
            _logger.Log(LogTypeEnum.Warning, $"[Update] Rolling back to {previousVersion}");
            _history.RecordRollback(previousVersion);

            _config.UpdateConfig(c =>
            {
                c.CurrentVersion = previousVersion;
            });
        }

    }

}
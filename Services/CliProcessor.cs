using EGM.Core.Enums;
using EGM.Core.Interfaces;

namespace EGM.Core.Services
{
    public class CliProcessor : ICliProcessor
    {
        private readonly ILogger _logger;
        private readonly IStateManager _stateManager;
        private readonly IBillValidator _billValidator;
        private readonly IUpdateManager _updateManager;
        private readonly IConfigManager _configManager;
        private readonly ITimeZoneValidator _timeZoneValidator;

        public CliProcessor( ILogger logger, IStateManager stateManager, IBillValidator billValidator, IUpdateManager updateManager, IConfigManager configManager, ITimeZoneValidator timeZoneValidator)
        {
            _logger = logger;
            _stateManager = stateManager;
            _billValidator = billValidator;
            _updateManager = updateManager;
            _configManager = configManager;
            _timeZoneValidator = timeZoneValidator;
        }

        public void Run()
        {
            _logger.Log(LogTypeEnum.Info, "EGM Core Module Ready. Type 'help' for commands.");

            while (true)
            {
                Console.Write("\nEGM> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                ProcessCommand(input.Trim());
            }
        }

        private void ProcessCommand(string input)
        {
            _logger.Log(LogTypeEnum.Info, $"[CLI] Command received: {input}");

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "exit":
                        HandleExit();
                        break;

                    case "help":
                        PrintHelp();
                        break;

                    case "status":
                        PrintStatus();
                        break;

                    case "start_game":
                        _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Operator started game");
                        break;

                    case "stop_game":
                        _stateManager.TransitionTo(EGMStateEnum.IDLE, "Operator stopped game");
                        break;

                    case "signal":
                        HandleSignal(parts);
                        break;

                    case "update":
                        HandleUpdate(parts);
                        break;

                    case "device":
                        HandleDevice(parts);
                        break;

                    case "os":
                        HandleOsCommand(parts);
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type 'help' to see available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogTypeEnum.Error, $"[CLI] Command error: {ex.Message}");
            }
        }

        private void HandleExit()
        {
            _logger.Log(LogTypeEnum.Info, "Shutting down system...");
            _billValidator.Stop();
            Environment.Exit(0);
        }

        private void HandleSignal(string[] parts)
        {
            if (parts.Length >= 2 && parts[1].Equals("door_open", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log(LogTypeEnum.Warning, "Door opened – entering maintenance mode.");

                _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Door opened");
            }
            else
            {
                _logger.Log(LogTypeEnum.Warning,"Usage: signal door_open");
            }
        }

        private void HandleUpdate(string[] parts)
        {
            // Expected:
            // update --package "E:\EGM.Core\Data\update_pkg_1.1.6.txt"

            if (parts.Length >= 3 && parts[1].Equals("--package", StringComparison.OrdinalIgnoreCase))
            {
                var packagePath = parts[2].Trim('"');
                _updateManager.InstallPackage(packagePath);
            }
            else
            {
                _logger.Log(LogTypeEnum.Warning, "Usage: update --package <path>");
            }
        }

        private void HandleDevice(string[] parts)
        {
            // device bill_validator ack on
            // device bill_validator ack off

            if (parts.Length >= 4 && parts[1] == "bill_validator" &&  parts[2] == "ack")
            {
                bool shouldFail = parts[3].Equals("off", StringComparison.OrdinalIgnoreCase);

                _billValidator.SetSimulatedFailure(shouldFail);
            }
            else
            {
                _logger.Log(LogTypeEnum.Warning, "Usage: device bill_validator ack on/off");
            }
        }

        private void HandleOsCommand(string[] parts)
        {
            // os set-timezone Africa/Conakry

            if (parts.Length >= 3 &&
                parts[1].Equals("set-timezone", StringComparison.OrdinalIgnoreCase))
            {
                var newZone = string.Join(" ", parts.Skip(2)).Trim('"');

                if (_timeZoneValidator.ValidateTimeZone(newZone, out string error))
                {
                    var oldZone = _configManager.GetConfig().TimeZone;

                    _configManager.UpdateConfig(cfg => cfg.TimeZone = newZone);

                    _logger.Audit(actor: "Operator",action: "Set Timezone",oldValue: oldZone,newValue: newZone);
                }
                else
                {
                    _logger.Log(LogTypeEnum.Error, $"Timezone update failed: {error}");
                }
            }
            else
            {
                _logger.Log(LogTypeEnum.Warning, "Usage: os set-timezone <ZoneId>");
            }
        }

        private void PrintStatus()
        {
            var cfg = _configManager.GetConfig();

            Console.WriteLine($"""
                ---------------------------------
                State:              {_stateManager.CurrentState}
                Current Version:    {cfg.CurrentVersion}
                Last Known Good:    {cfg.LastKnownGoodVersion}
                Timezone:           {cfg.TimeZone}
                NTP Enabled:        {cfg.NtpEnabled}
                ---------------------------------
                """);
        }


        private static void PrintHelp()
        {
            Console.WriteLine("""
                Available Commands:

                start_game
                stop_game
                signal door_open
                update --package <path>
                device bill_validator ack on/off
                os set-timezone <ZoneId>
                status
                exit
                """);
        }
    }
}


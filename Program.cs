using EGM.Core.Enums;
using EGM.Core.Interfaces;
using EGM.Core.Persistence;
using EGM.Core.Services;
using EGM.Core.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EGM.Core
{
    // =======================================================================
    // CONSOLE (CLI) FRONT END - the original interactive entry point.
    // It builds the DI graph, starts the bill-validator heartbeat, wires the
    // OnStateChanged console alert, and then runs the interactive CLI loop.
    //
    // A SECOND front end now exists alongside this one: EGM.Api (an ASP.NET Core
    // Web API + React dashboard under EGM.Api\). That project reuses the *exact*
    // same EGM.Core services via the same interfaces - the only differences are
    // that it wraps ILogger in a CapturingLogger (so the browser can read log
    // lines) and it does NOT register/run ICliProcessor, because HTTP endpoints
    // replace the typed CLI commands as the operator interface.
    //
    // Nothing here is removed or disabled: `dotnet run` on THIS project still
    // launches the CLI exactly as before. The web UI is an additive, parallel
    // shell over the same core - run it with `dotnet run` inside EGM.Api\.
    // =======================================================================
    public class Program
    {
        public static void Main(string[] args)
        {
            // Setup DI Container
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ILogger, LoggerService>();
                    services.AddSingleton<IConfigManager, ConfigManager>();
                    services.AddSingleton<IInstallHistoryStore, InstallHistoryStore>();
                    services.AddSingleton<IStateManager, StateManager>();
                    services.AddSingleton<IBillValidator, BillValidatorService>();
                    services.AddSingleton<IPackageValidator, PackageValidator>();
                    services.AddSingleton<ITimeZoneValidator, TimeZoneValidator>();
                    services.AddSingleton<IUpdateManager, UpdateManager>();
                    services.AddSingleton<ICliProcessor, CliProcessor>();
                })
                .Build();

            var billValidator = host.Services.GetRequiredService<IBillValidator>();
            var stateManager = host.Services.GetRequiredService<IStateManager>();
            var cli = host.Services.GetRequiredService<ICliProcessor>();

            // Start background services
            billValidator.Start();

            stateManager.OnStateChanged += newState =>
            {
                var originalColor = Console.ForegroundColor;

                if (newState == EGMStateEnum.MAINTENANCE)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[ALERT] SYSTEM ENTERED MAINTENANCE MODE!");
                }
                else if (newState == EGMStateEnum.RUNNING)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[GAME] Game Started!");
                }

                Console.ForegroundColor = originalColor;
            };

            // Start CLI loop
            cli.Run();
        }
    }
}

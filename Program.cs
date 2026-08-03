using EGM.Core.Enums;
using EGM.Core.Interfaces;
using EGM.Core.Persistence;
using EGM.Core.Services;
using EGM.Core.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

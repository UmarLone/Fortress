using Fortress.Core.Contracts;
using Fortress.Core.Extensions;
using Fortress.Core.Services;
using Fortress.Service.Infrastructure;
using Fortress.Service.Pipes;
using Fortress.Service.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Fortress.Service;

/// <summary>
/// Centralises all host configuration � logging, paths, and DI registrations.
/// Called by <see cref="Program.CreateHostBuilder"/>.
///
/// Run modes
/// ─────────
///   Console  dotnet run --project Fortress.Service
///   (also triggers when a debugger is attached or the process is interactive)
///   Service  Installed via sc.exe; started by the Windows Service Control Manager.
///        Use a non-interactive session without --console and without a debugger.
/// </summary>
public static class Startup
{
    public const string ServiceName = "Fortress Vault Service";

    // ── Detect run mode ───────────────────────────────────────────────────────
    public static bool IsConsoleMode(string[] args) =>
      args.Contains("--console") ||
        Environment.UserInteractive ||
        System.Diagnostics.Debugger.IsAttached;

    // ── App configuration ─────────────────────────────────────────────────────
    public static void ConfigureAppConfiguration(
  HostBuilderContext ctx, IConfigurationBuilder config)
    {
        config
            .SetBasePath(AppContext.BaseDirectory)
       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
   .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json",
    optional: true, reloadOnChange: false)
 .AddEnvironmentVariables(prefix: "FORTRESS_");
    }

    // ── Logging ───────────────────────────────────────────────────────────────
    public static void ConfigureLogging(
        HostBuilderContext ctx, ILoggingBuilder logging, string[] args)
    {
        logging.ClearProviders();

  if (IsConsoleMode(args))
        {
         // Coloured single-line output for the developer console
       logging.AddSimpleConsole(o =>
    {
      o.SingleLine      = true;
                o.TimestampFormat = "HH:mm:ss ";
 o.ColorBehavior   = LoggerColorBehavior.Enabled;
       });
     }
        else
  {
     // Windows Event Log for the installed service
         logging.AddConsole();
     logging.AddEventLog(o => o.SourceName = ServiceName);
        }

    // Honour log-level overrides from appsettings / environment
   logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
    }

    // ── Services (DI) ─────────────────────────────────────────────────────────
    public static void ConfigureServices(
    HostBuilderContext ctx, IServiceCollection services, string[] args)
    {
        var cfg       = ctx.Configuration;
        var isConsole = IsConsoleMode(args);

   // ── Paths ─────────────────────────────────────────────────────────────
        // Console mode  ? %LOCALAPPDATA%\Fortress  (writable without elevation)
        // Service mode  ? %ProgramData%\Fortress   (accessible by SYSTEM account)
      var baseDir = isConsole
   ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "Fortress")
            : Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Fortress");

    // appsettings.json overrides (leave empty to use the computed defaults above)
        var dbDir = cfg["Fortress:DatabaseDirectory"] is { Length: > 0 } d
            ? d
            : Path.Combine(baseDir, "Databases");

        var prefsDir = cfg["Fortress:PreferencesDirectory"] is { Length: > 0 } p
          ? p
            : Path.Combine(baseDir, "Service");

 // ── Preference service ────────────────────────────────────────────────
   services.AddSingleton<IPreferenceService>(_ => new FilePreferenceService(prefsDir));

    // ?? Vault settings reader (reads vault.settings.json written by the desktop app) ??
        services.AddSingleton<VaultSettingsReader>();

        // ── Fortress.Core (storage, health, intelligence, �) ─────────────────
        services.AddFortressCore(dbDir);

    // ── Infrastructure ────────────────────────────────────────────────────
   services.AddSingleton<VaultSessionService>();
 services.AddSingleton<AutofillHandler>();
  services.AddSingleton<VaultItemHandler>();

        // ── Named-pipe IPC server ─────────────────────────────────────────────
        services.AddSingleton<PipeServer>();
services.AddHostedService(sp => sp.GetRequiredService<PipeServer>());

        // ── Background workers ────────────────────────────────────────────────
        services.AddHostedService<HealthSnapshotWorker>();
    services.AddHostedService<EventLogTrimWorker>();

        // ── Startup banner (console mode only) ────────────────────────────────
        if (isConsole)
      services.AddHostedService(_ => new ConsoleBannerService(dbDir, prefsDir));
    }
}

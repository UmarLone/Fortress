using Fortress.Core.Contracts;
using Fortress.Core.Extensions;
using Fortress.Core.Security;
using Fortress.Windows.Desktop.Services;
using Fortress.Windows.Desktop.Services.Cloud;
using Fortress.Windows.Desktop.ViewModels.Pages;
using Fortress.Windows.Desktop.ViewModels.Windows;
using Fortress.Windows.Desktop.Views.Pages;
using Fortress.Windows.Desktop.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Fortress.Windows.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
          .ConfigureAppConfiguration(c =>
            {
                c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory));
                c.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
         {
             var cfg = context.Configuration;

             services.AddNavigationViewPageProvider();
             services.AddHostedService<ApplicationHostService>();

             // Core WPF-UI services
             services.AddSingleton<IThemeService, ThemeService>();
             services.AddSingleton<ITaskBarService, TaskBarService>();
             services.AddSingleton<INavigationService, NavigationService>();
             services.AddSingleton<ISnackbarService, SnackbarService>();
             services.AddSingleton<IContentDialogService, ContentDialogService>();

             // ── IPC / Session ────────────────────────────────────────────────
             // The desktop is a client of Fortress.Service.  It never opens the
             // LiteDB file directly — all vault data goes through the named pipe.
             services.AddSingleton<PipeClient>();
             services.AddSingleton<IDesktopSessionStore, DesktopSessionStore>();
             services.AddSingleton<IVaultSessionService, VaultSessionService>();
             services.AddSingleton<IBiometricService, BiometricService>();

             // ── Data service ─────────────────────────────────────────────────
             // Switch between real LiteDB storage and in-memory mock data via
             // appsettings.json: "UseMockData": true  (default = false → real)
             var useMock = cfg.GetValue<bool>("UseMockData");
             if (useMock)
             {
                 services.AddSingleton<IDesktopDataService, MockDataService>();
             }
             else
             {
                 // Pipe-backed: desktop talks to the service just like the extension.
                 // No IDataStorageService, no direct DB path.
                 services.AddSingleton<IDesktopDataService, PipeBackedDataService>();
             }

             // IVaultCryptoService is needed by BackupSyncViewModel for export/import
             // crypto on the local machine (snapshot encryption). The session key is
             // set by VaultSessionService on unlock; the service uses its own keys
             // for vault DB encryption. Registering this here unblocks navigation
             // to the Backup & Sync page — without it, ActivatorUtilities throws
             // "Unable to resolve service for type 'IVaultCryptoService'".
             services.AddSingleton<Fortress.Core.Security.IVaultCryptoService,
                                   Fortress.Core.Security.VaultCryptoService>();

             // Main window
             services.AddSingleton<INavigationWindow, MainWindow>();
             services.AddSingleton<MainWindowViewModel>();

             // Onboarding + Lock
             services.AddSingleton<OnboardingWindow>();
             services.AddSingleton<OnboardingViewModel>();
             services.AddSingleton<SetupWindow>();
             services.AddSingleton<SetupViewModel>();
             services.AddSingleton<LockScreenWindow>();
             services.AddSingleton<LockScreenViewModel>();

             // Pages + ViewModels
             services.AddSingleton<DashboardPage>();
             services.AddSingleton<DashboardViewModel>();
             services.AddSingleton<CredentialsPage>();
             services.AddSingleton<CredentialsViewModel>();
             services.AddSingleton<CreditCardsPage>();
             services.AddSingleton<CreditCardsViewModel>();
             services.AddSingleton<IdentitiesPage>();
             services.AddSingleton<IdentitiesViewModel>();
             services.AddSingleton<SecureItemsPage>();
             services.AddSingleton<SecureItemsViewModel>();
             services.AddSingleton<AuthenticatorsPage>();
             services.AddSingleton<AuthenticatorsViewModel>();
             services.AddSingleton<VaultHealthPage>();
             services.AddSingleton<VaultHealthViewModel>();
             services.AddSingleton<ActivityLogPage>();
             services.AddSingleton<ActivityLogViewModel>();
             services.AddSingleton<NotificationsPage>();
             services.AddSingleton<NotificationsViewModel>();
             services.AddSingleton<GroupsPage>();
             services.AddSingleton<GroupsViewModel>();
             services.AddSingleton<PasswordGeneratorPage>();
             services.AddSingleton<PasswordGeneratorViewModel>();
             services.AddSingleton<SettingsPage>();
             services.AddSingleton<SettingsViewModel>();

             // Cloud sync — credentials bound from appsettings.json via IOptions<T>
             services.Configure<GoogleDriveOptions>(cfg.GetSection(GoogleDriveOptions.Section));
             services.Configure<DropboxOptions>(cfg.GetSection(DropboxOptions.Section));
             services.Configure<OneDriveOptions>(cfg.GetSection(OneDriveOptions.Section));

             services.AddSingleton<WpfGoogleDriveSyncService>(sp =>
                 new WpfGoogleDriveSyncService(
                 new System.Net.Http.HttpClient(),
                  sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleDriveOptions>>()));

             services.AddSingleton<WpfDropboxSyncService>(sp =>
                    new WpfDropboxSyncService(
                         new System.Net.Http.HttpClient(),
                           sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DropboxOptions>>()));

             services.AddSingleton<WpfOneDriveSyncService>(sp =>
                 new WpfOneDriveSyncService(
                     new System.Net.Http.HttpClient(),
             sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OneDriveOptions>>()));

             services.AddSingleton<BackupSyncPage>();
             services.AddSingleton<BackupSyncViewModel>();
             services.AddSingleton<AboutPage>();
             services.AddSingleton<AboutViewModel>();
         }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}

using Controls.UserDialogs.Maui;
using Camera.MAUI;
using CommunityToolkit.Maui;
using DryIoc.ImTools;
using FFImageLoading.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Services;
using Fortress.Services;
using Fortress.ViewModels;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views;
using Fortress.Views.PopupPages;
using IeuanWalker.Maui.Switch;
using Maui.Biometric;
using MauiIcons.Core;
using MauiIcons.FontAwesome;
using MauiIcons.FontAwesome.Brand;
using MauiIcons.Material;
using Microsoft.Maui.LifecycleEvents;
using NReco.Logging.File;
using Plugin.LocalNotification;
using Shiny.Jobs;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using System.Diagnostics;
using The49.Maui.BottomSheet;
using ZXing.Net.Maui.Controls;
using INotificationService = Fortress.Mobile.Core.Contracts.INotificationService;

namespace Fortress.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp
               .CreateBuilder()
                  .UseMauiApp<App>()
                  .UseMauiIconsCore()
                  .UseFontAwesomeMauiIcons()
                     .UseFontAwesomeBrandMauiIcons()
            .UseMaterialMauiIcons()
                   .UseBottomSheet()
                    .UsePrism(new DryIocContainerExtension(), prism =>
              {
                  prism.RegisterTypes(containerRegistry =>
     {
         // ── Services that ViewModels depend on ──────────────────────────────────
         // NOTE: IBottomSheetService is registered in builder.Services (MS DI) below,
         // NOT here — so BottomSheetService gets the MS DI IServiceProvider which
         // has BottomSheet/BottomSheetViewModel registered and can resolve them.
         containerRegistry.RegisterSingleton<IUnlockService, UnlockService>();
         containerRegistry.RegisterSingleton<IEventAggregator, Prism.Events.EventAggregator>();
         containerRegistry.RegisterSingleton<ICredentialResolver, CredentialResolver>();
         containerRegistry.RegisterSingleton<ICryptographyService, CryptographyService>();
         containerRegistry.RegisterSingleton<IEventLogProcessor, EventLogProcessor>();
         containerRegistry.RegisterSingleton<IDataStorageService, SqliteStorage>();
        
         containerRegistry.RegisterInstance<IAppInfo>(AppInfo.Current);
         containerRegistry.RegisterInstance<IConnectivity>(Connectivity.Current);
         containerRegistry.RegisterInstance<IFileSystem>(FileSystem.Current);
         containerRegistry.RegisterInstance<IVersionTracking>(VersionTracking.Default);
#if ANDROID
         containerRegistry.Register<IDeviceServices, Fortress.Droid.Services.DeviceService>();
#endif
#if IOS
         containerRegistry.Register<IDeviceServices, Fortress.iOS.Services.iOSDeviceService>();
#endif
         // ── Pages ───────────────────────────────────────────────────────────────
         containerRegistry.RegisterForNavigation<GroupsPage,GroupsPageViewModel>();
         containerRegistry.RegisterForNavigation<GroupMembersPage, GroupMembersPageViewModel>();
         containerRegistry.RegisterForNavigation<UnlockPage, UnlockPageViewModel>();
         containerRegistry.RegisterForNavigation<HelpPage, HelpPageViewModel>();
         containerRegistry.RegisterForNavigation<AboutPage, AboutPageViewModel>();
         containerRegistry.RegisterForNavigation<MenuPage, MenuPageViewModel>();
         containerRegistry.RegisterForNavigation<NotificationPage, NotificationPageViewModel>();
         containerRegistry.RegisterForNavigation<NotificationDetailPage, NotificationDetailPageViewModel>();
         containerRegistry.RegisterForNavigation<CredentialsPage, CredentialsPageViewModel>();
         containerRegistry.RegisterForNavigation<HomePage, HomePageViewModel>();
         containerRegistry.RegisterForNavigation<OnboardingPage, OnboardingPageViewModel>();
         containerRegistry.RegisterForNavigation<SetupPage, SetupPageViewModel>();
         containerRegistry.RegisterForNavigation<PasswordGeneratorPage, PasswordGeneratorPageViewModel>();
         containerRegistry.RegisterForNavigation<AuthenticatorsPage, AuthenticatorsPageViewModel>();
         containerRegistry.RegisterForNavigation<PlaceholderPage>();
         containerRegistry.RegisterForNavigation<QrCodePage, QrCodePageViewModel>();
         containerRegistry.RegisterForNavigation<LocalLogsPage, LocalLogsPageViewModel>();
         containerRegistry.RegisterForNavigation<CloudSyncPage, CloudSyncPageViewModel>();
         containerRegistry.RegisterForNavigation<GoogleDriveSyncPage, GoogleDriveSyncPageViewModel>();
         containerRegistry.RegisterForNavigation<DropboxSyncPage, DropboxSyncPageViewModel>();
         containerRegistry.RegisterForNavigation<OneDriveSyncPage, OneDriveSyncPageViewModel>();
         containerRegistry.RegisterForNavigation<UsbExportPage, UsbExportPageViewModel>();
         containerRegistry.RegisterForNavigation<CreditCardsPage, CreditCardsPageViewModel>();
         containerRegistry.RegisterForNavigation<AddEditCredentialPage, AddEditCredentialPageViewModel>();
         containerRegistry.RegisterForNavigation<AddEditCreditCardPage, AddEditCreditCardPageViewModel>();
         containerRegistry.RegisterForNavigation<AddEditAuthenticatorPage, AddEditAuthenticatorPageViewModel>();
         containerRegistry.RegisterForNavigation<IdentitiesPage, IdentitiesPageViewModel>();
         containerRegistry.RegisterForNavigation<AddEditIdentityPage, AddEditIdentityPageViewModel>();
         // ── Unified Secure Items (ID docs, Wi-Fi, SSH) ──────────────────────
         containerRegistry.RegisterForNavigation<SecureItemsPage, SecureItemsPageViewModel>();
         containerRegistry.RegisterForNavigation<AddEditSecureItemPage, AddEditSecureItemPageViewModel>();
         containerRegistry.RegisterForNavigation<VaultHealthPage, VaultHealthPageViewModel>();
         // ── Import ──────────────────────────────────────────────────────────────
         containerRegistry.RegisterForNavigation<ImportPage, ImportPageViewModel>();
         containerRegistry.RegisterForNavigation<PasskeysPage, PasskeysPageViewModel>();
         containerRegistry.RegisterForNavigation<PasskeyDetailPage, PasskeyDetailPageViewModel>();
         containerRegistry.RegisterForNavigation<ActivityLogPage, ActivityLogPageViewModel>();
         containerRegistry.RegisterForNavigation<SecurityInsightsPage, SecurityInsightsViewModel>();
         // ── Detail pages ───────────────────────────────────────────────────
         containerRegistry.RegisterForNavigation<CredentialDetailPage, CredentialDetailPageViewModel>();
         containerRegistry.RegisterForNavigation<AuthenticatorDetailPage, AuthenticatorDetailPageViewModel>();
         // ── Share / Receive vault items ────────────────────────────────────
         containerRegistry.RegisterForNavigation<ShareItemPage, ShareItemPageViewModel>();
         containerRegistry.RegisterForNavigation<ReceiveItemPage, ReceiveItemPageViewModel>();
         // ── Voice command sheet ────────────────────────────────────────────
         containerRegistry.Register<VoiceListeningSheet>();
         containerRegistry.Register<VoiceListeningSheetViewModel>();
     });
                  prism.OnInitialized(container =>
{
    var logger = container.Resolve<ILogger<App>>();
    logger.LogInformation("Prism initialized.");
})
.CreateWindow(async (container, nav) =>
      {
          var logger = container.Resolve<ILogger<App>>();
          logger.LogInformation("Prism window ready - navigating...");
          var a = await nav.NavigateAsync($"/{nameof(PlaceholderPage)}");
      });
              })
                     .ConfigureSyncfusionToolkit()
             .UseShiny()
             .UseSwitch()
            .UseUserDialogs(registerInterface: true, () => { })
             .UseMauiCommunityToolkit()
             .UseBarcodeReader()
           .UseMauiCameraView()
                .UseBiometricAuthentication()
                        .UseFFImageLoading()
                  .UseSkiaSharp()
                   .UseLocalNotification(config =>
                   {
                       config.AddCategory(new NotificationCategory(NotificationCategoryType.Event)
                       {
                           ActionList =
                  {
             new NotificationAction(100)
 {
    Title = "Approve",
           Android = new Plugin.LocalNotification.AndroidOption.AndroidAction
           {
   LaunchAppWhenTapped = false,
         IconName = new Plugin.LocalNotification.AndroidOption.AndroidIcon { ResourceName = "ic_stat_check" }
   },
        IOS = new Plugin.LocalNotification.iOSOption.iOSAction
      {
   Action = Plugin.LocalNotification.iOSOption.iOSActionType.None
       }
 },
   new NotificationAction(101)
   {
    Title = "Deny",
           Android = new Plugin.LocalNotification.AndroidOption.AndroidAction
      {
    LaunchAppWhenTapped = false,
    IconName = new Plugin.LocalNotification.AndroidOption.AndroidIcon { ResourceName = "ic_stat_close" }
  },
             IOS = new Plugin.LocalNotification.iOSOption.iOSAction
         {
   Action = Plugin.LocalNotification.iOSOption.iOSActionType.None
    }
       }
                   }
                       });
                   })
        .ConfigureMauiHandlers(handlers =>
                 {
#if IOS
                     Microsoft.Maui.Handlers.NavigationViewHandler.Mapper.AppendToMapping("SearchToolbar", (handler, view) =>
                         {
                             if (view is NavigationPage nav)
                                 Fortress.iOS.Services.NavigationPageExtensions.HookNavigationEvents(handler, nav);
                         });
#endif
                 })
            .ConfigureFonts(fonts =>
                     {
                         fonts.AddFont("BebasNeue-Regular.ttf", "BebasNeue");
                         fonts.AddFont("Teko-Regular.ttf", "Teko");
                         fonts.AddFont("Teko-Light.ttf", "TekoLight");
                         fonts.AddFont("Teko-Medium.ttf", "TekoMedium");
                         fonts.AddFont("Teko-SemiBold.ttf", "TekoSemiBold");
                         fonts.AddFont("Teko-Bold.ttf", "TekoBold");
                         fonts.AddFont("Oxanium-Bold.ttf", "OxaniumBold");
                         fonts.AddFont("Michroma-Regular.ttf", "MichromaRegular");
                         fonts.AddFont("Audiowide-Regular.ttf", "Audiowide");

                     });

            builder.ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                          {
                              Microsoft.Maui.ApplicationModel.Platform.Init(activity, bundle);
                          }));
#endif
                });

            builder.Services.AddSingleton<IAppInfo>(AppInfo.Current);
            builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
            builder.Services.AddSingleton<IDeviceDisplay>(DeviceDisplay.Current);
            builder.Services.AddSingleton<IFileSystem>(FileSystem.Current);
            builder.Services.AddSingleton<IPreferences>(Preferences.Default);
            builder.Services.AddSingleton<IVersionTracking>(VersionTracking.Default);
            builder.Services.AddSingleton(TimeProvider.System);

            builder.Configuration.AddJsonPlatformBundle();

            // ── Bind platform-specific appsettings sections to strongly-typed options ──
            builder.Services.Configure<GoogleDriveOptions>(
             builder.Configuration.GetSection(GoogleDriveOptions.Section));
            builder.Services.Configure<DropboxOptions>(
                 builder.Configuration.GetSection(DropboxOptions.Section));
            builder.Services.Configure<OneDriveOptions>(
                 builder.Configuration.GetSection(OneDriveOptions.Section));
            if (PreferenceWrapper.Instance.SendDiagnosticLogs)
            {
                builder.Services.AddLogging(loggingBuilder =>
                {

                    var logFilePath = System.IO.Path.Combine(FileSystem.Current.CacheDirectory, "fortress.log");

                    loggingBuilder.AddFile(logFilePath, fileLoggerOpts =>
                    {
                        fileLoggerOpts.FilterLogEntry = (logRecord) =>
                        {

                            return PreferenceWrapper.Instance.SendDiagnosticLogs;
                        };
                        fileLoggerOpts.FormatLogFileName = new Func<string, string>(fName => string.Format(fName, DateTime.UtcNow));
                        fileLoggerOpts.MaxRollingFiles = 5;
                        fileLoggerOpts.Append = true;
                        fileLoggerOpts.FileSizeLimitBytes = 10_000_000;
                        fileLoggerOpts.RollingFilesConvention = FileLoggerOptions.FileRollingConvention.Descending;
                        fileLoggerOpts.MinLevel = LogLevel.Warning;
                    });
                });
            }

#if DEBUG
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddDebug();
#endif

#if ANDROID
            builder.Services.AddSingleton<IDeviceServices, Fortress.Droid.Services.DeviceService>();
            builder.Services.AddSingleton<ISharedCredentialWriter>(sp => null);
#endif
#if IOS
            builder.Services.AddSingleton<IDeviceServices, Fortress.iOS.Services.iOSDeviceService>();
            builder.Services.AddSingleton<ISharedCredentialWriter, SharedCredentialWriter>();
#endif

           
            builder.Services.AddSingleton<IUnlockService, UnlockService>();
            builder.Services.AddSingleton<IEventAggregator, EventAggregator>();
            builder.Services.AddSingleton<ICredentialResolver, CredentialResolver>();
            builder.Services.AddSingleton<ICryptographyService, CryptographyService>();
            builder.Services.AddSingleton<IEventLogProcessor, EventLogProcessor>();
            builder.Services.AddSingleton<IDataStorageService, SqliteStorage>();
            builder.Services.AddSingleton<IBottomSheetService, BottomSheetService>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();

            // ── Vault Share (peer-to-peer encrypted file sharing) ────────────────
            builder.Services.AddSingleton<VaultShareService>();

            // ── Voice command services ───────────────────────────────────────────────
            builder.Services.AddSingleton<MlNetIntentClassifier>();
            builder.Services.AddSingleton<VoiceCommandParser>();
            builder.Services.AddSingleton<IVoiceCommandService, VoiceCommandService>();
#if ANDROID
            builder.Services.AddTransient<ISpeechToTextService, Fortress.Droid.Services.AndroidSpeechToTextService>();
#endif
#if IOS
            builder.Services.AddTransient<ISpeechToTextService, Fortress.iOS.Services.iOSSpeechToTextService>();
#endif

            // ── Vault health ─────────────────────────────────────────────────────────
            builder.Services.AddSingleton<VaultHealthConfig>();
            builder.Services.AddSingleton<PasswordAnomalyDetector>();
            builder.Services.AddSingleton<VaultHealthCalculator>();
            builder.Services.AddTransient<VaultHealthPage>();
            builder.Services.AddTransient<VaultHealthPageViewModel>();
            builder.Services.AddTransient<GroupsPage>();
            builder.Services.AddTransient<GroupsPageViewModel>();
            builder.Services.AddTransient<GroupMembersPage>();
            builder.Services.AddTransient<GroupMembersPageViewModel>();

            // ── Voice command sheet (transient — new instance per use) ───────────────
            builder.Services.AddTransient<VoiceListeningSheet>();
            builder.Services.AddTransient<VoiceListeningSheetViewModel>();

            // ── Cloud Sync ───────────────────────────────────────────────────────────
            builder.Services.AddSingleton<GoogleDriveSyncService>();
            builder.Services.AddSingleton<DropboxSyncService>();
            builder.Services.AddSingleton<OneDriveSyncService>();
            builder.Services.AddSingleton<Fortress.Mobile.Core.Services.CloudSyncScheduler>();

            // ── Import engine ─────────────────────────────────────────────────────────
            builder.Services.AddSingleton<IVaultImportService, VaultImportService>();
            builder.Services.AddTransient<ImportPage>();
            builder.Services.AddTransient<ImportPageViewModel>();

            // ── Cloud backup background job ──────────────────────────────────────────
            builder.Services.AddJob(new JobInfo(
           Fortress.Mobile.Core.Contracts.JobConstants.CloudBackupJob,
     typeof(Fortress.Mobile.Core.Services.Jobs.CloudBackupJob))
            {
                RequiredInternetAccess = InternetAccess.Any,
                RunOnForeground = false
            });

            // ── BottomSheet Views / ViewModels ───────────────────────────────────────
            builder.Services.AddTransient<Fortress.Views.PopupPages.SetUnlockPINSheet>();
            builder.Services.AddTransient<SetUnlockPINSheetViewModel>();
            builder.Services.AddTransient<Fortress.Views.PopupPages.BottomSheet>();
            builder.Services.AddTransient<BottomSheetViewModel>();
            builder.Services.AddTransient<Fortress.Views.PopupPages.ScanQrCodeSheet>();
            builder.Services.AddTransient<ScanQrCodeSheetViewModel>();
            // ── Alert / Confirm / Prompt sheet ───────────────────────────────────────
            builder.Services.AddTransient<Fortress.Views.PopupPages.AlertSheet>();
            builder.Services.AddTransient<AlertSheetViewModel>();

            // ── Intelligence Engine ──────────────────────────────────────────────────
            builder.Services.Add(ServiceDescriptor.Singleton(
     typeof(IDomainRiskAnalyzer),
        typeof(DomainRiskAnalyzer)));
            builder.Services.Add(ServiceDescriptor.Singleton(
                     typeof(IItemClassifier),
              typeof(ItemClassifier)));
            builder.Services.Add(ServiceDescriptor.Singleton(
           typeof(IVoiceCommandRouter),
            typeof(VoiceCommandRouter)));
            builder.Services.Add(ServiceDescriptor.Singleton(
                       typeof(AutofillSuggestionEngine),
                 typeof(AutofillSuggestionEngine)));

            // ── Have I Been Pwned ─────────────────────────────────────────────────────
            builder.Services.AddSingleton(client =>
               {
                   return new HttpClient()
                   {
                       BaseAddress = new Uri("https://haveibeenpwned.com/"),
                       Timeout = TimeSpan.FromSeconds(15)
                   };

               });
            builder.Services.AddSingleton<IHaveIBeenPwnedService, HaveIBeenPwnedService>();

            builder.Services.AddGeneratedServices();

            MauiApp app;
            try
            {
                app = builder.Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Build-time DI error: {ex}");
                throw;
            }

#if DEBUG
            try
            {
                var sp = app.Services;
                _ = sp.GetRequiredService<Microsoft.Maui.IApplication>();
                _ = sp.GetRequiredService<INavigationService>();
                _ = sp.GetRequiredService<IJobManager>();
                _ = sp.GetRequiredService<IDeviceServices>();
                _ = sp.GetRequiredService<IBottomSheetService>();
                _ = sp.GetRequiredService<IDataStorageService>();

                System.Diagnostics.Debug.WriteLine("🧪 Testing ViewModel resolution...");
                var setupVm = sp.GetRequiredService<SetupPageViewModel>();
                System.Diagnostics.Debug.WriteLine($"✅ SetupPageViewModel resolved: {setupVm != null}");
                var homeVm = sp.GetRequiredService<HomePageViewModel>();
                System.Diagnostics.Debug.WriteLine($"✅ HomePageViewModel via Prism: {homeVm != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ DI FAILURE: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
#endif
            return app;
        }
    }

}
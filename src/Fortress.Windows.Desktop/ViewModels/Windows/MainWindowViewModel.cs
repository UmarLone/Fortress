using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Fortress";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Dashboard",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Logins",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Key24 },
                TargetPageType = typeof(Views.Pages.CredentialsPage)
            },
            new NavigationViewItem()
            {
                Content = "Credit Cards",
                Icon = new SymbolIcon { Symbol = SymbolRegular.CreditCardPerson24 },
                TargetPageType = typeof(Views.Pages.CreditCardsPage)
            },
            new NavigationViewItem()
            {
                Content = "Identities",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                TargetPageType = typeof(Views.Pages.IdentitiesPage)
            },
            new NavigationViewItem()
            {
                Content = "Secure Items",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ShieldLock24 },
                TargetPageType = typeof(Views.Pages.SecureItemsPage)
            },
            new NavigationViewItem()
            {
                Content = "Authenticators",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ShieldKeyhole24 },
                TargetPageType = typeof(Views.Pages.AuthenticatorsPage)
            },
            new NavigationViewItem()
            {
                Content = "Vault Health",
                Icon = new SymbolIcon { Symbol = SymbolRegular.HeartPulse24 },
                TargetPageType = typeof(Views.Pages.VaultHealthPage)
            },
            new NavigationViewItem()
            {
                Content = "Activity Log",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ClipboardTextLtr24 },
                TargetPageType = typeof(Views.Pages.ActivityLogPage)
            },
            new NavigationViewItem()
            {
                Content = "Password Generator",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Password24 },
                TargetPageType = typeof(Views.Pages.PasswordGeneratorPage)
            },
            new NavigationViewItem()
            {
                Content = "Groups",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Folder24 },
                TargetPageType = typeof(Views.Pages.GroupsPage)
            },
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Notifications",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Alert24 },
                TargetPageType = typeof(Views.Pages.NotificationsPage)
            },
            new NavigationViewItem()
            {
                Content = "Backup & Sync",
                Icon = new SymbolIcon { Symbol = SymbolRegular.CloudArrowUp24 },
                TargetPageType = typeof(Views.Pages.BackupSyncPage)
            },
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            },
            new NavigationViewItem()
            {
                Content = "About",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(Views.Pages.AboutPage)
            },
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Open Fortress", Tag = "tray_home" },
            new MenuItem { Header = "Lock Vault", Tag = "tray_lock" }
        };
    }
}

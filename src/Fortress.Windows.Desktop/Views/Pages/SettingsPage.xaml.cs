using Fortress.Windows.Desktop.ViewModels.Pages;
using Fortress.Windows.Desktop.Views.Dialogs;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();

        // PIN row clicked — if PIN already enabled: disable it; otherwise open the setup dialog
        private async void OnPinRowClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.PinEnabled)
            {
                ViewModel.DisablePin();
                return;
            }

            var dialog = new PinSetupDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && dialog.ResultPin is { } pin)
                await ViewModel.SavePinAsync(pin);
        }

        // Windows Hello row clicked
        private async void OnHelloRowClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await ViewModel.ToggleBiometricAsyncCommand.ExecuteAsync(null);
        }
    }
}

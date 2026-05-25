using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class BackupSyncPage : INavigableView<BackupSyncViewModel>
    {
        public BackupSyncViewModel ViewModel { get; }

        public BackupSyncPage(BackupSyncViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();

        private void OnOneDriveClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ViewModel.SelectProviderCommand.Execute("onedrive");

        private void OnGoogleDriveClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ViewModel.SelectProviderCommand.Execute("googledrive");

        private void OnDropboxClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ViewModel.SelectProviderCommand.Execute("dropbox");
    }
}

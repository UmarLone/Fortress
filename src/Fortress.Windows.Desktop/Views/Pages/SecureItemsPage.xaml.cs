using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class SecureItemsPage : INavigableView<SecureItemsViewModel>
    {
        public SecureItemsViewModel ViewModel { get; }

        public SecureItemsPage(SecureItemsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();
    }
}

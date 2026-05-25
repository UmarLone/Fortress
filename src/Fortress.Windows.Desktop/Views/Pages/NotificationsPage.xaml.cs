using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class NotificationsPage : INavigableView<NotificationsViewModel>
    {
  public NotificationsViewModel ViewModel { get; }

        public NotificationsPage(NotificationsViewModel viewModel)
        {
  ViewModel = viewModel;
   DataContext = this;
            InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();
    }
}

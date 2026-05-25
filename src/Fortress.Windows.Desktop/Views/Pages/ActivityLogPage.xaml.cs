using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class ActivityLogPage : INavigableView<ActivityLogViewModel>
    {
        public ActivityLogViewModel ViewModel { get; }

      public ActivityLogPage(ActivityLogViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();
    }
}

using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class GroupsPage : INavigableView<GroupsViewModel>
    {
  public GroupsViewModel ViewModel { get; }

        public GroupsPage(GroupsViewModel viewModel)
        {
 ViewModel = viewModel;
            DataContext = this;
   InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
  public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();
}
}

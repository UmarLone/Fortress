using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class AboutPage : INavigableView<AboutViewModel>
    {
   public AboutViewModel ViewModel { get; }

        public AboutPage(AboutViewModel viewModel)
        {
        ViewModel = viewModel;
     DataContext = this;
 InitializeComponent();
        }

   public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
     public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();
 }
}

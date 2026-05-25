using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class AuthenticatorsPage : INavigableView<AuthenticatorsViewModel>
    {
   public AuthenticatorsViewModel ViewModel { get; }
    public AuthenticatorsPage(AuthenticatorsViewModel viewModel)
   { ViewModel = viewModel; DataContext = this; InitializeComponent(); }

 public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
  public async Task OnNavigatedFromAsync() => await ViewModel.OnNavigatedFromAsync();

        private void OnFaviconFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
   if (sender is System.Windows.Controls.Image img &&
      img.Parent is System.Windows.Controls.Border b)
                b.Visibility = System.Windows.Visibility.Collapsed;
  }
    }
}

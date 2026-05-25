using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class CredentialsPage : INavigableView<CredentialsViewModel>
    {
        public CredentialsViewModel ViewModel { get; }

        public CredentialsPage(CredentialsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        public async Task OnNavigatedToAsync() => await ViewModel.OnNavigatedToAsync();
        public Task OnNavigatedFromAsync() => ViewModel.OnNavigatedFromAsync();

        private void OnItemClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext is LoginItemViewModel vm)
            {
                if (ViewModel.IsSelectionMode)
                    ViewModel.ToggleItemSelectionCommand.Execute(vm);
                else
                    ViewModel.SelectItemCommand.Execute(vm);
            }
        }

        /// <summary>
        /// Hides the favicon Border when the image URL fails to load,
        /// revealing the initials fallback underneath.
        /// </summary>
        private void OnFaviconFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                // Walk up to the containing Border (FaviconBorder) and collapse it
                var parent = img.Parent as System.Windows.Controls.Border;
                if (parent != null) parent.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        /// <summary>Same handler for the large detail-panel favicon.</summary>
        private void OnDetailFaviconFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                var parent = img.Parent as System.Windows.Controls.Border;
                if (parent != null) parent.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}

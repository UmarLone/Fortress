using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class IdentitiesPage : INavigableView<IdentitiesViewModel>
    {
    public IdentitiesViewModel ViewModel { get; }
     public IdentitiesPage(IdentitiesViewModel viewModel)
        { ViewModel = viewModel; DataContext = this; InitializeComponent(); }
    }
}

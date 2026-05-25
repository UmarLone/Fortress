using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }
    }
}

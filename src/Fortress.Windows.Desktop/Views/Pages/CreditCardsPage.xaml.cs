using Fortress.Windows.Desktop.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.Views.Pages
{
    public partial class CreditCardsPage : INavigableView<CreditCardsViewModel>
    {
      public CreditCardsViewModel ViewModel { get; }

    public CreditCardsPage(CreditCardsViewModel viewModel)
   {
 ViewModel = viewModel;
  DataContext = this;
            InitializeComponent();
    }

        private void OnCardClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
       if (sender is System.Windows.FrameworkElement fe && fe.DataContext is CreditCardViewModel vm)
       ViewModel.SelectItemCommand.Execute(vm);
        }
    }
}

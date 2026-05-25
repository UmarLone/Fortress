
using Fortress.Mobile.Core.Models;
using Fortress.Services;
using Fortress.ViewModels;
using System.Linq;
namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LogFiltersSheet : The49.Maui.BottomSheet.BottomSheet
    {
        private LogFiltersSheetViewModel? _viewModel => BindingContext as LogFiltersSheetViewModel;

        public LogFiltersSheet()
        {
            InitializeComponent();
        }
 
    }
}
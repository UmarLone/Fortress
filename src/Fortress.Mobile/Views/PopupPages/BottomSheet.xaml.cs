using Fortress.ViewModels.PopupPagesViewModels;

namespace Fortress.Views.PopupPages
{
    public partial class BottomSheet : The49.Maui.BottomSheet.BottomSheet
    {
        private BottomSheetViewModel? _viewModel => BindingContext as BottomSheetViewModel;
        public BottomSheet()
        {
            InitializeComponent();
        }
       
    }
}
using Fortress.ViewModels;
namespace Fortress.Views.PopupPages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SetUnlockPINSheet : The49.Maui.BottomSheet.BottomSheet
    {
        private SetUnlockPINSheetViewModel? _viewModel => BindingContext as SetUnlockPINSheetViewModel;
        public SetUnlockPINSheet()
        {
            InitializeComponent();
        }
        
        private void Pin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;
       
            if (string.IsNullOrWhiteSpace(_viewModel.Pin) || _viewModel.Pin.Length != 4)
            {
                txtPinError.IsVisible = true;
                txtPinError.Text = "PIN must be 4 digits";
            }
            else
            {
                txtPinError.IsVisible = false;
            }

            _viewModel.CanExecute = !string.IsNullOrWhiteSpace(_viewModel.Pin) && 
              !string.IsNullOrWhiteSpace(_viewModel.ConfirmPin) &&
           _viewModel.Pin == _viewModel.ConfirmPin &&
             _viewModel.Pin.Length == 4;
        }

        private void ConfirmPin_TextChanged(object sender, TextChangedEventArgs e)
        {
        if (_viewModel == null) return;
         
            if (string.IsNullOrWhiteSpace(_viewModel.ConfirmPin))
         {
     txtConfirmPinError.IsVisible = true;
   txtConfirmPinError.Text = "Please confirm your PIN";
     }
       else if (_viewModel.ConfirmPin != _viewModel.Pin)
{
    txtConfirmPinError.IsVisible = true;
txtConfirmPinError.Text = "PINs do not match";
            }
         else
  {
     txtConfirmPinError.IsVisible = false;
  }

      _viewModel.CanExecute = !string.IsNullOrWhiteSpace(_viewModel.Pin) && 
     !string.IsNullOrWhiteSpace(_viewModel.ConfirmPin) &&
      _viewModel.Pin == _viewModel.ConfirmPin &&
       _viewModel.Pin.Length == 4;
  }
    }
}
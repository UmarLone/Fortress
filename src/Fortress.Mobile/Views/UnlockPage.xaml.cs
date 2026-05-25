using Fortress.ViewModels;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class UnlockPage : ContentPage
    {
        private UnlockPageViewModel? _viewModel => BindingContext as UnlockPageViewModel;

        /// <summary>
        /// When true, the next Pin_TextChanged is a programmatic clear
        /// after a failed attempt — don't overwrite the error state.
        /// </summary>
        private bool _suppressNextTextChanged;

        public UnlockPage()
        {
            InitializeComponent();
        }

        private void Pin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;

            // If the ViewModel just cleared the PIN after a failed attempt,
            // skip this one event so the error message stays visible.
            if (_suppressNextTextChanged)
            {
                _suppressNextTextChanged = false;
                btnVerifyPin.IsEnabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(_viewModel.Pin) || _viewModel.Pin.Length != 4)
            {
                // Only show a validation hint while the user is actively typing.
                // Don't touch HasError if it's already showing a server/verify error.
                if (!_viewModel.HasError || string.IsNullOrEmpty(e.OldTextValue))
                {
                    _viewModel.HasError = _viewModel.Pin?.Length > 0;
                    _viewModel.ErrorMessage = _viewModel.Pin?.Length > 0 ? "PIN must be 4 digits" : string.Empty;
                }
                btnVerifyPin.IsEnabled = false;
            }
            else
            {
                _viewModel.HasError = false;
                _viewModel.ErrorMessage = string.Empty;
                btnVerifyPin.IsEnabled = true;
            }
        }

        /// <summary>
        /// Called from the ViewModel (via MessagingCenter or a simple flag)
        /// to indicate the next PIN clear is a failed-attempt reset.
        /// We expose this so the ViewModel can signal it.
        /// </summary>
        public void SuppressNextTextChanged()
        {
            _suppressNextTextChanged = true;
        }

        private void ContentPage_Loaded(object sender, EventArgs e)
        {
            if (_viewModel != null && _viewModel.CanUsePin)
            {
                txtPin.Focus();
            }
        }
    }
}
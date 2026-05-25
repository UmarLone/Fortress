using Fortress.ViewModels.PopupPagesViewModels;

namespace Fortress.Views.PopupPages
{
    public partial class ScanQrCodeSheet : The49.Maui.BottomSheet.BottomSheet
    {
        private ScanQrCodeSheetViewModel? _viewModel => BindingContext as ScanQrCodeSheetViewModel;

        public ScanQrCodeSheet()
        {
            InitializeComponent();
        }

        private void scannerView_BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
        {
            // Take only the first result and guard against null early —
            // avoids any allocation on frames where nothing was detected.
            var result = e.Results?.FirstOrDefault();
            if (result == null) return;

            // Do NOT add another BeginInvokeOnMainThread here.
            // ExecuteOnBarcodeScannedCommand already posts to the main thread
            // internally. Wrapping again creates a nested async-in-Action pattern
            // where the inner await is orphaned (no debugger = silent swallow).
            if (_viewModel?.QrCodeResultCommand.CanExecute(result) == true)
                _viewModel.QrCodeResultCommand.Execute(result);
        }
    }
}
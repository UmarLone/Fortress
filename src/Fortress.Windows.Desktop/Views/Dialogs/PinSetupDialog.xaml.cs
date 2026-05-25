using Wpf.Ui.Controls;

namespace Fortress.Windows.Desktop.Views.Dialogs
{
    public partial class PinSetupDialog : FluentWindow
    {
        /// <summary>The confirmed PIN value — only set when the user clicks Save.</summary>
        public string? ResultPin { get; private set; }

        public PinSetupDialog()
        {
   InitializeComponent();
        }

     private void OnPinChanged(object sender, RoutedEventArgs e) => Validate();
    private void OnConfirmChanged(object sender, RoutedEventArgs e) => Validate();

     private void Validate()
        {
        var pin     = PinBox.Password;
            var confirm = ConfirmBox.Password;

       // PIN length check
            bool pinOk = pin.Length >= 4 && pin.Length <= 6 && pin.All(char.IsAsciiDigit);
      PinError.Visibility = (!string.IsNullOrEmpty(pin) && !pinOk)
         ? System.Windows.Visibility.Visible
         : System.Windows.Visibility.Collapsed;

            // Confirm match check
            bool matchOk = confirm == pin;
            ConfirmError.Visibility = (!string.IsNullOrEmpty(confirm) && !matchOk)
       ? System.Windows.Visibility.Visible
         : System.Windows.Visibility.Collapsed;

            SaveButton.IsEnabled = pinOk && matchOk && !string.IsNullOrEmpty(confirm);
   }

        private void OnSave(object sender, System.Windows.RoutedEventArgs e)
     {
     ResultPin = PinBox.Password;
     DialogResult = true;
   Close();
        }

     private void OnCancel(object sender, System.Windows.RoutedEventArgs e)
   {
        DialogResult = false;
    Close();
        }
    }
}

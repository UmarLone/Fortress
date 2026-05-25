using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.ViewModels;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CredentialsPage : ContentPage
    {
        private CancellationTokenSource? _longPressCts;
        private const int LongPressThresholdMs = 500;

        public CredentialsPage()
        {
            InitializeComponent();
            // The hero header provides its own back button and title.
            // The NavigationPage bar is not needed on this page.
            NavigationPage.SetHasNavigationBar(this, false);
        }

        /// <summary>
        /// Starts a timer when the user presses down on a card.
        /// If the timer fires before the finger is lifted, we treat it as a
        /// long-press and invoke the ViewModel's LongPressItemCommand (which
        /// enters multi-select mode and selects the item).
        /// </summary>
        private async void OnItemButtonPressed(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var token = _longPressCts.Token;

            var credential = (sender as Button)?.CommandParameter as CredentialView;
            if (credential is null) return;

            try
            {
                await Task.Delay(LongPressThresholdMs, token);

                // Timer completed without cancellation → long-press detected.
                // Suppress the normal tap Command by removing it temporarily.
                if (sender is Button btn)
                    btn.Command = null;

                if (BindingContext is CredentialsPageViewModel vm)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        vm.LongPressItemCommand.Execute(credential));
                }
            }
            catch (TaskCanceledException)
            {
                // Finger was lifted before the threshold → normal tap.
                // The Button's Command binding handles it.
            }
        }

        /// <summary>
        /// Cancels the long-press timer when the finger lifts.
        /// Also restores the Command binding if it was cleared by a long-press.
        /// </summary>
        private void OnItemButtonReleased(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = null;

            // Restore the Command after a long-press cleared it.
            // The next Pressed/Released cycle will re-bind automatically via XAML,
            // but we force it here in case the same Button instance is reused.
            if (sender is Button btn && btn.Command is null && BindingContext is CredentialsPageViewModel vm)
            {
                btn.SetBinding(Button.CommandProperty,
                    new Binding("BindingContext.ShowOptionsCommand",
                        source: this));
                btn.CommandParameter = btn.CommandParameter; // keep existing parameter
            }
        }
    }
}

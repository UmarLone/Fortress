using Fortress.ViewModels;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CreditCardsPage : ContentPage
    {
        private CancellationTokenSource? _longPressCts;
        private const int LongPressThresholdMs = 500;

        public CreditCardsPage()
        {
            InitializeComponent();
        }

        private async void OnItemButtonPressed(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var token = _longPressCts.Token;

            var item = (sender as Button)?.CommandParameter as CreditCardItemViewModel;
            if (item is null) return;

            try
            {
                await Task.Delay(LongPressThresholdMs, token);
                if (sender is Button btn) btn.Command = null;
                if (BindingContext is CreditCardsPageViewModel vm)
                    MainThread.BeginInvokeOnMainThread(() => vm.LongPressItemCommand.Execute(item));
            }
            catch (TaskCanceledException) { }
        }

        private void OnItemButtonReleased(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = null;

            if (sender is Button btn && btn.Command is null && BindingContext is CreditCardsPageViewModel)
            {
                btn.SetBinding(Button.CommandProperty,
                    new Binding("BindingContext.CardOptionsCommand", source: this));
                btn.CommandParameter = btn.CommandParameter;
            }
        }
    }
}

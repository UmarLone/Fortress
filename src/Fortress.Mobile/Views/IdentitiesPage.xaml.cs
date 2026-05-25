using Fortress.ViewModels;

namespace Fortress.Views
{
    public partial class IdentitiesPage : ContentPage
    {
        private CancellationTokenSource? _longPressCts;
        private const int LongPressThresholdMs = 500;

        public IdentitiesPage() => InitializeComponent();

        private async void OnItemButtonPressed(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var token = _longPressCts.Token;

            var item = (sender as Button)?.CommandParameter as IdentityItemViewModel;
            if (item is null) return;

            try
            {
                await Task.Delay(LongPressThresholdMs, token);
                if (sender is Button btn) btn.Command = null;
                if (BindingContext is IdentitiesPageViewModel vm)
                    MainThread.BeginInvokeOnMainThread(() => vm.LongPressItemCommand.Execute(item));
            }
            catch (TaskCanceledException) { }
        }

        private void OnItemButtonReleased(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = null;

            if (sender is Button btn && btn.Command is null && BindingContext is IdentitiesPageViewModel)
            {
                btn.SetBinding(Button.CommandProperty,
                  new Binding("BindingContext.ShowOptionsCommand", source: this));
                btn.CommandParameter = btn.CommandParameter;
            }
        }
    }
}

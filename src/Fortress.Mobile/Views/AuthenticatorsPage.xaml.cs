using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Services;
using Fortress.ViewModels;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AuthenticatorsPage : ContentPage, ISecurePage
    {
        private AuthenticatorsPageViewModel? ViewModel => BindingContext as AuthenticatorsPageViewModel;
        private readonly IEventAggregator _eventAggregator;
        private CancellationTokenSource? _longPressCts;
        private const int LongPressThresholdMs = 500;

        public AuthenticatorsPage(IEventAggregator eventAggregator)
        {
            InitializeComponent();
            // Navigation bar is hidden — the hero header provides back + title.
            NavigationPage.SetHasNavigationBar(this, false);
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<AuthenticatorAddedEvent>().Subscribe(OnAuthenticatorAdded);
        }

        private void OnAuthenticatorAdded(Authenticator authenticator)
        {
            MainThread.BeginInvokeOnMainThread(() =>
          {
              if (ViewModel == null) return;
              var auth = ViewModel.AuthenticatorsView.FirstOrDefault(x => x.Id == authenticator.Id);
              if (auth == null) return;
              var cv = this.FindByName<CollectionView>("AuthenticatorsCollectionView");
              cv?.ScrollTo(auth, position: ScrollToPosition.MakeVisible, animate: true);
          });
        }

        protected override void OnDisappearing()
        {
            _eventAggregator.GetEvent<AuthenticatorAddedEvent>().Unsubscribe(OnAuthenticatorAdded);
            base.OnDisappearing();
        }

        private async void OnItemButtonPressed(object? sender, EventArgs e)
        {
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var token = _longPressCts.Token;

            var item = (sender as Button)?.CommandParameter as Authenticator;
          if (item is null) return;

            try
      {
      await Task.Delay(LongPressThresholdMs, token);
         if (sender is Button btn) btn.Command = null;
    if (BindingContext is AuthenticatorsPageViewModel vm)
               MainThread.BeginInvokeOnMainThread(() => vm.LongPressItemCommand.Execute(item));
     }
  catch (TaskCanceledException) { }
        }

        private void OnItemButtonReleased(object? sender, EventArgs e)
        {
     _longPressCts?.Cancel();
            _longPressCts = null;

        if (sender is Button btn && btn.Command is null && BindingContext is AuthenticatorsPageViewModel)
         {
   btn.SetBinding(Button.CommandProperty,
       new Binding("BindingContext.ShowOptionsCommand", source: this));
           btn.CommandParameter = btn.CommandParameter;
 }
        }
    }
}
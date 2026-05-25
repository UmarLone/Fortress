using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    public class SetupInputSheetViewModel : BottomSheetViewModelBase
    {
        private readonly IUserDialogs _dialogService;

        private string url;
        public string Url
        {
            get { return url; }
            set { SetProperty(ref url, value); }
        }
        private string email;
        public string Email
        {
            get { return email; }
            set { SetProperty(ref email, value); }
        }
        private bool _canExecute;
        public bool CanExecute
        {
            get { return _canExecute; }
            set { SetProperty(ref _canExecute, value); }
        }
        public SetupInputSheetViewModel(
            IUserDialogs dialogService)
        {
            _dialogService = dialogService;
        }

        public override Task InitializeAsync(object args, string title)
        {
            return Task.CompletedTask;
        }
        public DelegateCommand DoneCommand =>
         _doneCommand ??= new DelegateCommand(ExecuteDoneCommand);
        private DelegateCommand _doneCommand;

        private async void ExecuteDoneCommand()
        {
            using (_dialogService.Loading("Verifying please wait..."))
            {
                

                ReturnResult?.Invoke((Url, Email));
                DismissAction?.Invoke();
            }
        }
    }
    public class SetupInputSheetArgs
    {
        public string InitialUrl { get; set; }
        public string InitialEmail { get; set; }
    }
}

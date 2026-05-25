using Fortress.Extensions;
using Prism.Mvvm;
using Prism.Navigation;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class ViewModelBase : BindableBase, IInitialize, INavigationAware, IDestructible
    {
        protected INavigationService NavigationService { get; private set; }

        private string _title;
        public string Title
        {
      get => _title;
            set => SetProperty(ref _title, value);
        }

        // ── Navigation ────────────────────────────────────────────────────────
        private AsyncCommand? _goBackCommand;
        public ICommand GoBackCommand =>
     _goBackCommand ??= new AsyncCommand(async () => await NavigationService.GoBackAsync());

     public ViewModelBase(INavigationService navigationService)
     {
         NavigationService = navigationService;
        }

        public virtual void HandleIsActiveTrue(object sender, EventArgs e) { }
        public virtual void HandleIsActiveFalse(object sender, EventArgs e) { }
  public virtual void Initialize(INavigationParameters parameters) { }
        public virtual void OnNavigatedFrom(INavigationParameters parameters) { }
        public virtual void OnNavigatedTo(INavigationParameters parameters) { }
        public virtual void Destroy() { }
}
}

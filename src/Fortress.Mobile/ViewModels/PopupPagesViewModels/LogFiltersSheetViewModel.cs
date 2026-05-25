using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Models;
namespace Fortress.ViewModels
{
    public class LogFiltersSheetViewModel : BottomSheetViewModelBase
    {
        #region Properties

        private LogFilter logFilter = new LogFilter()
        {
            StartDate = DateTime.Now.AddMonths(-1).ToUniversalTime(),
            EndDate = DateTime.Now.ToUniversalTime().Date.Add(new TimeSpan(23, 59, 59)),
            RecordCount = 100,
            EventTypes = new ObservableCollection<EventDisplayItem>(),

        };

        public LogFilter LogFilter
        {
            get { return logFilter; }
            set { SetProperty(ref logFilter, value); }
        }
        #endregion
        public LogFiltersSheetViewModel()
        {
        }
        public override Task InitializeAsync(object args, string title)
        {
            if (args is LogFilter f)
            {
                LogFilter = new LogFilter
                {
                    StartDate = f.StartDate,
                    EndDate = f.EndDate,
                    RecordCount = f.RecordCount,
                    EventTypes = new ObservableCollection<EventDisplayItem>(f.EventTypes.OrderBy(x => x.DisplayName)),
                    SelectedEventTypes = new ObservableCollection<EventDisplayItem>(f.SelectedEventTypes.OrderBy(x => x.DisplayName))
                };
            }
            return Task.CompletedTask;
        }
        
        private DelegateCommand _applyFilterCommand;
        public DelegateCommand ApplyFilterCommand => _applyFilterCommand ??= new DelegateCommand(ApplyFilterCommandAsync);

        private async void ApplyFilterCommandAsync()
        {
            ReturnResult?.Invoke((LogFilter));
            DismissAction?.Invoke();
        }
    }
}

using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Fortress.Mobile.Core.Models
{
    public class LogFilter : BindableBase
    {

        private ObservableCollection<EventDisplayItem> eventTypes= new ObservableCollection<EventDisplayItem>();
        public ObservableCollection<EventDisplayItem> EventTypes
        {
            get { return eventTypes; }
            set { SetProperty(ref eventTypes, value); }
        }
        private ObservableCollection<EventDisplayItem> selectedEventTypes= new ObservableCollection<EventDisplayItem>();
        public ObservableCollection<EventDisplayItem> SelectedEventTypes
        {
            get { return selectedEventTypes; }
            set { SetProperty(ref selectedEventTypes, value); }
        }
        private int recordCount = 100;
        public int RecordCount
        {
            get { return recordCount; }
            set { SetProperty(ref recordCount, value); }
        }
        private List<int> recordOptions = new List<int>() { 100, 500, 1000, 5000 };
        public List<int> RecordOptions
        {
            get { return recordOptions; }
            set { SetProperty(ref recordOptions, value); }
        }
        private DateTime startDate = DateTime.Now.AddMonths(-1).ToUniversalTime();
        public DateTime StartDate
        {
            get { return startDate; }
            set { SetProperty(ref startDate, value); }
        }
        private DateTime endDate = DateTime.Now.ToUniversalTime().Date.Add(new TimeSpan(23, 59, 59));
        public DateTime EndDate
        {
            get { return endDate; }
            set { SetProperty(ref endDate, value); }
        }
    }
    public class EventDisplayItem:BindableBase
    {
        private int key;
        public int Key
        {
            get { return key; }
            set { SetProperty(ref key, value); }
        }
        private string displayName;
        public  string DisplayName
        {
            get { return displayName; }
            set { SetProperty(ref displayName, value); }
        }
    }
}

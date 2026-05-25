using Fortress.Mobile.Core.Contracts;

namespace Fortress.ViewModels
{
    // ── SyncScheduleOption – shared by both provider ViewModels ──────────────
    public sealed class SyncScheduleOption : Prism.Mvvm.BindableBase
    {
        public SyncSchedule Value { get; }
        public string Label { get; }
        public string Description { get; }
        public string IconGlyph { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SyncScheduleOption(SyncSchedule value)
        {
            Value = value;
            Label = value.ToDisplayString();
            (Description, IconGlyph) = value switch
            {
                SyncSchedule.Manual => ("You choose when to back up", "\uE425"),
                SyncSchedule.Hourly => ("Best for frequent vault changes", "\uE192"),
                SyncSchedule.Daily => ("Recommended – set and forget", "\uE8DF"),
                SyncSchedule.Weekly => ("Good balance of freshness & battery", "\uE616"),
                SyncSchedule.Monthly => ("Minimal background activity", "\uE878"),
                _ => ("", "\uE8DF")
            };
        }
    }
}

using System.Windows.Input;

namespace Fortress.Models
{
    // ── Item kinds ─────────────────────────────────────────────────────────────

    public enum SettingItemKind { SectionHeader, Toggle, Nav, Spacer }

    // ── Base ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Flat item that drives a single row in the settings CollectionView.
    /// The DataTemplateSelector maps <see cref="Kind"/> to the right XAML template.
    /// </summary>
    public class SettingItem : Prism.Mvvm.BindableBase
    {
        public SettingItemKind Kind { get; init; }

        // ── Section header ──────────────────────────────────────────────────
        public string? SectionTitle { get; init; }

        // ── Shared icon / label fields ──────────────────────────────────────
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string IconGlyph { get; init; } = string.Empty;
        public string IconBg { get; init; } = "#E0E7FF";
        public string IconFg { get; init; } = "#407CCA";

        // ── Toggle-specific ─────────────────────────────────────────────────
        /// <summary>Unique key forwarded to <c>SettingChangedCommand</c> when the row is tapped.</summary>
        public string? SwitchClassId { get; init; }

        private bool _isToggled;
        public bool IsToggled
        {
            get => _isToggled;
            set => SetProperty(ref _isToggled, value);
        }

        /// <summary>
        /// When non-null, called by <c>RefreshToggle()</c> to pull the
        /// authoritative value back from the VM after an async operation may
        /// have changed it (e.g. biometric auth fail, dialog cancel).
        /// </summary>
        public Func<bool>? ToggledValueReader { get; init; }

        /// <summary>Pulls the current value from <see cref="ToggledValueReader"/> if available.</summary>
        public void RefreshToggle()
        {
            if (ToggledValueReader != null)
                IsToggled = ToggledValueReader();
        }

        /// <summary>
        /// When non-null, called by the VM to refresh the subtitle text
        /// of DynNav rows without re-executing the tap command.
        /// </summary>
        public Func<string>? SubtitleReader { get; init; }

        /// <summary>Pulls fresh subtitle text from <see cref="SubtitleReader"/> if available.</summary>
        public void RefreshSubtitle()
        {
            if (SubtitleReader != null)
                DynamicSubtitle = SubtitleReader();
        }

        /// <summary>
        /// When non-null the row is only shown when this returns <c>true</c>.
        /// Evaluated once when the section list is built and again whenever
        /// <see cref="RefreshVisibility"/> is called from the VM.
        /// </summary>
        public Func<bool>? VisibilityCondition { get; init; }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        // ── Nav-specific ────────────────────────────────────────────────────
        public string? SubtitleBinding { get; init; }   // dynamic subtitle text (set by VM)

        private string? _dynamicSubtitle;
        public string? DynamicSubtitle
        {
            get => _dynamicSubtitle;
            set => SetProperty(ref _dynamicSubtitle, value);
        }

        /// <summary>Tapped command for nav rows (and the outer tap on toggle rows).</summary>
        public ICommand? TapCommand { get; set; }

        // ── Helpers ─────────────────────────────────────────────────────────

        public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle ?? DynamicSubtitle);
        public string EffectiveSubtitle => DynamicSubtitle ?? Subtitle ?? string.Empty;

        public void RefreshVisibility()
        {
            if (VisibilityCondition != null)
                IsVisible = VisibilityCondition();
        }
    }
}

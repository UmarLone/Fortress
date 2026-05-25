using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fortress.Models
{
    /// <summary>One FAQ entry inside a help category.</summary>
    public class HelpItem : INotifyPropertyChanged
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>A group of related FAQ items shown under one accordion header.</summary>
    public class HelpCategory
    {
        public string Category { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public List<HelpItem> Items { get; set; } = new();
    }

    // ── Flat row types for the virtualised CollectionView ─────────────────────
    public enum HelpRowKind { CategoryHeader, Question, Footer }

    /// <summary>
    /// Flat row that the CollectionView DataTemplateSelector maps to a template.
    /// Avoids BindableLayout (non-virtualised) by pre-flattening categories + items.
    /// </summary>
    public class HelpRow
    {
        public HelpRowKind Kind { get; init; }

        // CategoryHeader
        public string? CategoryTitle { get; init; }
        public string? CategoryIcon { get; init; }

        // Question
        public HelpItem? Item { get; init; }

        // Footer – no extra data needed
    }
}

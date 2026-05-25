using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Models
{
    /// <summary>
/// Represents one filter chip in the Logins page header bar.
    /// Index 0 is always "All"; the rest are user-defined VaultGroups.
    /// Mirrors the pattern used in Fortress.Mobile CredentialsPageViewModel.
    /// </summary>
    public partial class CredentialFilterChip : ObservableObject
    {
        public string Label { get; set; } = "All";
        public Guid? GroupId { get; set; }

   /// <summary>Hex colour assigned to the group, e.g. "#3B82F6". Null for the "All" chip.</summary>
        public string? Color { get; set; }

  /// <summary>Pre-loaded member credential IDs. Null means show all.</summary>
 public HashSet<Guid>? MemberIds { get; set; }

        [ObservableProperty]
        private bool _isActive;

        partial void OnIsActiveChanged(bool value)
      {
     OnPropertyChanged(nameof(ChipBackground));
            OnPropertyChanged(nameof(ChipForeground));
     OnPropertyChanged(nameof(ChipBorderBrush));
      }

        private static readonly SolidColorBrush FallbackActiveBg =
      new(System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6));

        public Brush ChipBackground
        {
            get
  {
         if (!IsActive) return new SolidColorBrush(
             System.Windows.Media.Color.FromArgb(0x14, 0x00, 0x00, 0x00));

             if (Color is { Length: > 0 })
         {
try
        {
  return new SolidColorBrush(
                 (System.Windows.Media.Color)ColorConverter.ConvertFromString(Color));
         }
            catch { }
     }
 return FallbackActiveBg;
            }
        }

        public Brush ChipForeground =>
 IsActive
 ? Brushes.White
             : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0x00, 0x00));

        public Brush ChipBorderBrush
        {
          get
   {
     if (!IsActive) return new SolidColorBrush(
        System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));

  if (Color is { Length: > 0 })
         {
          try
      {
         return new SolidColorBrush(
              (System.Windows.Media.Color)ColorConverter.ConvertFromString(Color));
      }
   catch { }
 }
         return FallbackActiveBg;
   }
      }

        /// <summary>Small colour dot visible next to group-chip labels.</summary>
        public bool HasGroupColor => GroupId != null && !string.IsNullOrEmpty(Color);

        public Brush DotBrush
        {
            get
          {
                if (!HasGroupColor) return Brushes.Transparent;
           try
         {
             return new SolidColorBrush(
     (System.Windows.Media.Color)ColorConverter.ConvertFromString(Color!));
     }
             catch { return Brushes.Transparent; }
       }
}
    }
}

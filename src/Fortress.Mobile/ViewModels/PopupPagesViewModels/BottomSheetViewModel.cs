using Fortress.Mobile.Adapters;
using MauiIcons.Core;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    public class BottomSheetViewModel : BottomSheetViewModelBase
    {
        private string _title;
        public string Title
        {
            get { return _title; }
            set
            {
                SetProperty(ref _title, value);

            }
        }
        private int _height;
        public int Height
        {
            get { return _height; }
            set
            {
                SetProperty(ref _height, value);

            }
        }
        private ObservableCollection<BottomSheetOption> bottomSheetOptions = new ObservableCollection<BottomSheetOption>();

        public ObservableCollection<BottomSheetOption> BottomSheetOptions
        {
            get { return bottomSheetOptions; }
            set { SetProperty(ref bottomSheetOptions, value); }
        } 
        public override Task InitializeAsync(object args, string title)
        {
            var data = (List<BottomSheetOption>)args;
             Title = title;

            BottomSheetOptions = new ObservableCollection<BottomSheetOption>(data);
            if (BottomSheetOptions.Count > 3)
                Height = BottomSheetOptions.Count * 65;
            else if (BottomSheetOptions.Count == 3)
                Height = BottomSheetOptions.Count * 85;
            else
                Height = BottomSheetOptions.Count * 100;
            return Task.CompletedTask;
        }
        
        private async void ExecuteRunCommand(object obj)
        {
            var option = obj as BottomSheetOption;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                option.Action?.Invoke();
                DismissAction?.Invoke();
            });
        }
        private DelegateCommand<object> _runCommand;

        public DelegateCommand<object> RunCommand => _runCommand ?? new DelegateCommand<object>(ExecuteRunCommand);
    }
    public sealed class BottomSheetOption
    {
        public string Title { get; set; }

        /// <summary>
        /// Material icon glyph — bound to a Label with FontFamily="MaterialIconsRegular".
        /// </summary>
        public string IconGlyph { get; set; } = string.Empty;

        /// <summary>True when this option has an icon glyph to display.</summary>
     public bool HasIcon => !string.IsNullOrEmpty(IconGlyph);

    /// <summary>
        /// Badge background colour.  Null = use the theme default (PrimaryLightestColor).
        /// Set explicitly for per-option colours (e.g. red for Delete).
        /// </summary>
      public Color IconBadgeColor { get; set; }

        /// <summary>Badge icon colour.  Null = use the theme default (PrimaryColor).</summary>
        public Color IconColor { get; set; }

   /// <summary>
        /// Legacy write-only setter — kept so callers that pass a MauiIcon still work.
        /// The Unicode glyph is extracted from the enum's [Description] attribute.
        /// Prefer setting IconGlyph directly with the local MaterialIcons constants.
        /// </summary>
        public MauiIcon Icon
        {
            set
      {
   if (value?.Icon is not System.Enum e) return;
      try
  {
        var fieldInfo = e.GetType().GetField(e.ToString());
       if (fieldInfo?.GetCustomAttributes(
        typeof(System.ComponentModel.DescriptionAttribute), false)
    .FirstOrDefault() is System.ComponentModel.DescriptionAttribute attr
       && attr.Description.Length > 0)
          {
          IconGlyph = attr.Description;
            }
   }
    catch { /* ignore — IconGlyph stays empty */ }
            }
        }

        public bool IsBrandTypeFont { get; set; }
        public Action Action { get; set; }
public bool IsSelected { get; set; }
 }
}

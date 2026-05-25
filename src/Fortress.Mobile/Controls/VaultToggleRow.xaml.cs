using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultToggleRow : ContentView
    {
        // ── IconGlyph ──────────────────────────────────────────────────────────
        public static readonly BindableProperty IconGlyphProperty =
         BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(VaultToggleRow), string.Empty);
 public string IconGlyph
     {
        get => (string)GetValue(IconGlyphProperty);
         set => SetValue(IconGlyphProperty, value);
 }

      // ── IconColor ──────────────────────────────────────────────────────────
   public static readonly BindableProperty IconColorProperty =
         BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(VaultToggleRow), Colors.Gray);
        public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
   set => SetValue(IconColorProperty, value);
        }

        // ── IconBackgroundColor ────────────────────────────────────────────────
        public static readonly BindableProperty IconBackgroundColorProperty =
  BindableProperty.Create(nameof(IconBackgroundColor), typeof(Color), typeof(VaultToggleRow), Colors.Transparent);
        public Color IconBackgroundColor
        {
       get => (Color)GetValue(IconBackgroundColorProperty);
            set => SetValue(IconBackgroundColorProperty, value);
        }

  // ── Title ──────────────────────────────────────────────────────────────
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(VaultToggleRow), string.Empty);
        public string Title
     {
            get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
      }

        // ── Subtitle ───────────────────────────────────────────────────────────
        public static readonly BindableProperty SubtitleProperty =
   BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(VaultToggleRow), string.Empty,
       propertyChanged: (b, _, __) => ((VaultToggleRow)b).RaisePropertyChanged(nameof(HasSubtitle)));
        public string Subtitle
     {
    get => (string)GetValue(SubtitleProperty);
         set => SetValue(SubtitleProperty, value);
    }

   /// <summary>True when Subtitle has content — used to collapse the subtitle label automatically.</summary>
        public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

        // ── IsToggled ──────────────────────────────────────────────────────────
        public static readonly BindableProperty IsToggledProperty =
      BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(VaultToggleRow), false, BindingMode.TwoWay);
        public bool IsToggled
        {
   get => (bool)GetValue(IsToggledProperty);
            set => SetValue(IsToggledProperty, value);
        }

      // ── ShowSeparator ──────────────────────────────────────────────────────
        public static readonly BindableProperty ShowSeparatorProperty =
            BindableProperty.Create(nameof(ShowSeparator), typeof(bool), typeof(VaultToggleRow), false);
   public bool ShowSeparator
  {
            get => (bool)GetValue(ShowSeparatorProperty);
  set => SetValue(ShowSeparatorProperty, value);
        }

        // ── SwitchClassId ─────────────────────────────────────────────────────
        /// <summary>Forwarded to the inner Switch.ClassId so MenuPage can identify which setting changed.</summary>
        public static readonly BindableProperty SwitchClassIdProperty =
    BindableProperty.Create(nameof(SwitchClassId), typeof(string), typeof(VaultToggleRow), string.Empty);
  public string SwitchClassId
        {
            get => (string)GetValue(SwitchClassIdProperty);
      set => SetValue(SwitchClassIdProperty, value);
        }

        // ── ToggledCommand ────────────────────────────────────────────────────
        /// <summary>
        /// Executed when the user taps the row. The command parameter is SwitchClassId.
        /// Replaces the Toggled event — bind this directly to SettingChangedCommand
        /// in the page XAML so no code-behind is needed.
 /// </summary>
 public static readonly BindableProperty ToggledCommandProperty =
     BindableProperty.Create(nameof(ToggledCommand), typeof(System.Windows.Input.ICommand), typeof(VaultToggleRow), null);
        public System.Windows.Input.ICommand ToggledCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(ToggledCommandProperty);
        set => SetValue(ToggledCommandProperty, value);
        }

   // ── Toggled event ──────────────────────────────────────────────────────
      public event EventHandler<bool> Toggled;

   public VaultToggleRow()
        {
   InitializeComponent();
        }

      private void Row_Tapped(object sender, TappedEventArgs e)
        {
            IsToggled = !IsToggled;
  // Fire event for code-behind subscribers
        Toggled?.Invoke(this, IsToggled);
 // Execute command with SwitchClassId as parameter
if (ToggledCommand?.CanExecute(SwitchClassId) == true)
   ToggledCommand.Execute(SwitchClassId);
  }

        private void RaisePropertyChanged(string propertyName)
            => OnPropertyChanged(propertyName);
    }
}

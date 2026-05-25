using System.Windows.Input;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultGradientButton : ContentView
    {
        // ── Text ──────────────────────────────────────────────────────────────
        public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(VaultGradientButton), string.Empty);
        public string Text
  {
            get => (string)GetValue(TextProperty);
     set => SetValue(TextProperty, value);
        }

        // ── Command ───────────────────────────────────────────────────────────
        public static readonly BindableProperty CommandProperty =
         BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(VaultGradientButton), null);
        public ICommand Command
        {
    get => (ICommand)GetValue(CommandProperty);
 set => SetValue(CommandProperty, value);
        }

        // ── IsLoading ─────────────────────────────────────────────────────────
        public static readonly BindableProperty IsLoadingProperty =
   BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(VaultGradientButton), false);
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
         set => SetValue(IsLoadingProperty, value);
        }

        // ── HeightRequest (shadows base) ──────────────────────────────────────
        public new static readonly BindableProperty HeightRequestProperty =
            BindableProperty.Create(nameof(HeightRequest), typeof(double), typeof(VaultGradientButton), 56.0);
 public new double HeightRequest
        {
      get => (double)GetValue(HeightRequestProperty);
   set => SetValue(HeightRequestProperty, value);
        }

        // ── ColorStart ────────────────────────────────────────────────────────
    // Default is Colors.Transparent – overridden in OnHandlerChanged once
        // theme resources are available, so GradientStop is never null on Android.
        public static readonly BindableProperty ColorStartProperty =
   BindableProperty.Create(
    nameof(ColorStart), typeof(Color), typeof(VaultGradientButton),
    Colors.Transparent,
   propertyChanged: (b, _, n) => ((VaultGradientButton)b).ApplySolidColor());
    public Color ColorStart
    {
    get => (Color)GetValue(ColorStartProperty);
            set => SetValue(ColorStartProperty, value);
   }

        // ── ColorEnd ──────────────────────────────────────────────────────────
        public static readonly BindableProperty ColorEndProperty =
  BindableProperty.Create(
 nameof(ColorEnd), typeof(Color), typeof(VaultGradientButton),
        Colors.Transparent,
         propertyChanged: (b, _, n) => ((VaultGradientButton)b).ApplySolidColor());
        public Color ColorEnd
     {
            get => (Color)GetValue(ColorEndProperty);
     set => SetValue(ColorEndProperty, value);
      }

        // ── CornerRadius ──────────────────────────────────────────────────────
      public static readonly BindableProperty CornerRadiusProperty =
      BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(VaultGradientButton), 16.0);
        public double CornerRadius
      {
    get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        // ── IsEnabled (overrides base) – dims button when false ──────────────
        public new static readonly BindableProperty IsEnabledProperty =
        BindableProperty.Create(
   nameof(IsEnabled), typeof(bool), typeof(VaultGradientButton), true,
    propertyChanged: (b, _, n) => ((VaultGradientButton)b).ApplyEnabledState((bool)n));
        public new bool IsEnabled
      {
     get => (bool)GetValue(IsEnabledProperty);
    set => SetValue(IsEnabledProperty, value);
        }

        public VaultGradientButton()
        {
        InitializeComponent();
        }

    // Resolve theme fallback once the handler (and thus the visual tree) is ready.
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler != null)
    ApplySolidColor();
        }

    // Sets the button to a solid PrimaryColor background.
        // ColorStart is kept for API compatibility but no longer drives a gradient.
   private void ApplySolidColor()
        {
            var color = ResolveColor(ColorStart, "PrimaryColor");
    // The XAML now uses BackgroundColor directly; just sync the shadow.
      if (ShadowBrush != null)
ShadowBrush.Brush = new SolidColorBrush(color);
        }

    // Returns the supplied color if it is not Transparent/default,
        // otherwise looks up the named DynamicResource from the app dictionary.
        private static Color ResolveColor(Color candidate, string resourceKey)
     {
  if (candidate != Colors.Transparent)
         return candidate;

       if (Application.Current?.Resources.TryGetValue(resourceKey, out var raw) == true
                && raw is Color c)
   return c;

            return Colors.DodgerBlue; // last-resort non-null fallback
  }

        private void ApplyEnabledState(bool enabled)
     {
   Opacity = enabled ? 1.0 : 0.45;
    // Also disable the inner Button so taps are blocked
  var btn = this.FindByName<Button>("InnerButton");
    if (btn != null) btn.IsEnabled = enabled;
  }
    }
}

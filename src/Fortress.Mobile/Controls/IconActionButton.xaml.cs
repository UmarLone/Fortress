using System.Windows.Input;

namespace Fortress.Controls
{
 [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class IconActionButton : ContentView
    {
    // ── IconGlyph ──────────────────────────────────────────────────────────
     public static readonly BindableProperty IconGlyphProperty =
      BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(IconActionButton), string.Empty);

   public string IconGlyph
      {
 get => (string)GetValue(IconGlyphProperty);
       set => SetValue(IconGlyphProperty, value);
        }

   // ── IconColor ──────────────────────────────────────────────────────────
 public static readonly BindableProperty IconColorProperty =
 BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(IconActionButton), Colors.White);

        public Color IconColor
        {
     get => (Color)GetValue(IconColorProperty);
    set => SetValue(IconColorProperty, value);
        }

      // ── IconSize ───────────────────────────────────────────────────────────
 public static readonly BindableProperty IconSizeProperty =
  BindableProperty.Create(nameof(IconSize), typeof(double), typeof(IconActionButton), 22.0);

    public double IconSize
        {
          get => (double)GetValue(IconSizeProperty);
     set => SetValue(IconSizeProperty, value);
        }

        // ── BackgroundColor (shadows the base property intentionally) ──────────
        public static new readonly BindableProperty BackgroundColorProperty =
   BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(IconActionButton), Colors.Transparent);

  public new Color BackgroundColor
 {
 get => (Color)GetValue(BackgroundColorProperty);
  set => SetValue(BackgroundColorProperty, value);
        }

        // ── CornerRadius ───────────────────────────────────────────────────────
        public static readonly BindableProperty CornerRadiusProperty =
         BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(IconActionButton), 12.0);

    public double CornerRadius
    {
       get => (double)GetValue(CornerRadiusProperty);
           set => SetValue(CornerRadiusProperty, value);
        }

        // ── Size ───────────────────────────────────────────────────────────────
        public static readonly BindableProperty SizeProperty =
    BindableProperty.Create(nameof(Size), typeof(double), typeof(IconActionButton), 44.0);

      public double Size
        {
    get => (double)GetValue(SizeProperty);
   set => SetValue(SizeProperty, value);
        }

     // ── Command ────────────────────────────────────────────────────────────
   public static readonly BindableProperty CommandProperty =
    BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(IconActionButton), null);

        public ICommand Command
        {
get => (ICommand)GetValue(CommandProperty);
    set => SetValue(CommandProperty, value);
        }

        // ── CommandParameter ───────────────────────────────────────────────────
    public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(IconActionButton), null);

    public object CommandParameter
        {
   get => GetValue(CommandParameterProperty);
     set => SetValue(CommandParameterProperty, value);
        }

        public IconActionButton()
        {
     InitializeComponent();
        }
    }
}

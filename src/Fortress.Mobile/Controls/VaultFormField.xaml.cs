namespace Fortress.Controls
{
    /// <summary>Identifies which in-place formatter TextWatcher to attach on Android.</summary>
    public enum EntryFormatterType { None, CardNumber, Expiry }

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultFormField : ContentView
    {
        // ── IconGlyph ──────────────────────────────────────────────────────────
        public static readonly BindableProperty IconGlyphProperty =
            BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(VaultFormField), string.Empty);
        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        // ── IconColor ──────────────────────────────────────────────────────────
        public static readonly BindableProperty IconColorProperty =
            BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(VaultFormField), Colors.Gray);
        public Color IconColor
        {
            get => (Color)GetValue(IconColorProperty);
            set => SetValue(IconColorProperty, value);
        }

        // ── IconBackgroundColor ────────────────────────────────────────────────
        public static readonly BindableProperty IconBackgroundColorProperty =
            BindableProperty.Create(nameof(IconBackgroundColor), typeof(Color), typeof(VaultFormField), Colors.Transparent);
        public Color IconBackgroundColor
        {
            get => (Color)GetValue(IconBackgroundColorProperty);
            set => SetValue(IconBackgroundColorProperty, value);
        }

        // ── FieldLabel ─────────────────────────────────────────────────────────
        public static readonly BindableProperty FieldLabelProperty =
            BindableProperty.Create(nameof(FieldLabel), typeof(string), typeof(VaultFormField), string.Empty);
        public string FieldLabel
        {
            get => (string)GetValue(FieldLabelProperty);
            set => SetValue(FieldLabelProperty, value);
        }

        // ── Placeholder ────────────────────────────────────────────────────────
        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(VaultFormField), string.Empty);
        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        // ── Text ───────────────────────────────────────────────────────────────
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(VaultFormField), string.Empty,
                BindingMode.TwoWay);
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        // ── IsPassword ─────────────────────────────────────────────────────────
        public static readonly BindableProperty IsPasswordProperty =
            BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(VaultFormField), false);
        public bool IsPassword
        {
            get => (bool)GetValue(IsPasswordProperty);
            set => SetValue(IsPasswordProperty, value);
        }

        // ── Keyboard ───────────────────────────────────────────────────────────
        public static readonly BindableProperty KeyboardProperty =
            BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(VaultFormField), Keyboard.Default);
        public Keyboard Keyboard
        {
            get => (Keyboard)GetValue(KeyboardProperty);
            set => SetValue(KeyboardProperty, value);
        }

        // ── MaxLength ──────────────────────────────────────────────────────────
        public static readonly BindableProperty MaxLengthProperty =
            BindableProperty.Create(nameof(MaxLength), typeof(int), typeof(VaultFormField), int.MaxValue);
        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        // ── HasError ───────────────────────────────────────────────────────────
        public static readonly BindableProperty HasErrorProperty =
            BindableProperty.Create(nameof(HasError), typeof(bool), typeof(VaultFormField), false);
        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        // ── ErrorMessage ───────────────────────────────────────────────────────
        public static readonly BindableProperty ErrorMessageProperty =
            BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(VaultFormField), string.Empty);
        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        // ── FormatterType ───────────────────────────────────────────────────────
        public static readonly BindableProperty FormatterTypeProperty =
            BindableProperty.Create(nameof(FormatterType), typeof(EntryFormatterType), typeof(VaultFormField), EntryFormatterType.None);
        public EntryFormatterType FormatterType
        {
            get => (EntryFormatterType)GetValue(FormatterTypeProperty);
            set => SetValue(FormatterTypeProperty, value);
        }

        public VaultFormField()
        {
            InitializeComponent();
        }

        // Raised when the inner Entry text changes – lets code-behind apply
        // formatting without the ViewModel setter needing to do it.
        public event EventHandler<TextChangedEventArgs> TextChanged;

        // Called by the Entry inside the XAML via x:Name or EventToCommand.
        // We expose a method so the XAML Entry can wire up directly.
        internal void OnEntryTextChanged(object sender, TextChangedEventArgs e)
            => TextChanged?.Invoke(this, e);
    }
}

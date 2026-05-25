using System.Windows.Input;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultPageHero : ContentView
    {
        // ── GradientStart ──────────────────────────────────────────────────
        public static readonly BindableProperty GradientStartProperty =
 BindableProperty.Create(nameof(GradientStart), typeof(Color), typeof(VaultPageHero), Colors.Transparent);
        public Color GradientStart
        {
 get => (Color)GetValue(GradientStartProperty);
     set => SetValue(GradientStartProperty, value);
    }

        // ── GradientEnd ────────────────────────────────────────────────────
        public static readonly BindableProperty GradientEndProperty =
            BindableProperty.Create(nameof(GradientEnd), typeof(Color), typeof(VaultPageHero), Colors.Transparent);
  public Color GradientEnd
 {
            get => (Color)GetValue(GradientEndProperty);
            set => SetValue(GradientEndProperty, value);
        }

        // ── Title ──────────────────────────────────────────────────────────
        public static readonly BindableProperty TitleProperty =
   BindableProperty.Create(nameof(Title), typeof(string), typeof(VaultPageHero), string.Empty);
   public string Title
      {
        get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
     }

     // ── Subtitle ───────────────────────────────────────────────────────
        public static readonly BindableProperty SubtitleProperty =
         BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(VaultPageHero), "VAULT");
        public string Subtitle
        {
       get => (string)GetValue(SubtitleProperty);
   set => SetValue(SubtitleProperty, value);
        }

     // ── CountText ──────────────────────────────────────────────────────
        public static readonly BindableProperty CountTextProperty =
            BindableProperty.Create(nameof(CountText), typeof(string), typeof(VaultPageHero), string.Empty);
        public string CountText
 {
    get => (string)GetValue(CountTextProperty);
         set => SetValue(CountTextProperty, value);
     }

        // ── IconGlyph (back-compat, no longer rendered) ────────────────────
        public static readonly BindableProperty IconGlyphProperty =
  BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(VaultPageHero), "\uE897");
     public string IconGlyph
      {
            get => (string)GetValue(IconGlyphProperty);
          set => SetValue(IconGlyphProperty, value);
        }

   // ── BackCommand ────────────────────────────────────────────────────
     public static readonly BindableProperty BackCommandProperty =
    BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(VaultPageHero), null);
        public ICommand BackCommand
        {
      get => (ICommand)GetValue(BackCommandProperty);
         set => SetValue(BackCommandProperty, value);
        }

        // ── SearchEnabled – set False on pages that don't need search ──────
        public static readonly BindableProperty SearchEnabledProperty =
   BindableProperty.Create(nameof(SearchEnabled), typeof(bool), typeof(VaultPageHero), true);
   public bool SearchEnabled
        {
      get => (bool)GetValue(SearchEnabledProperty);
     set => SetValue(SearchEnabledProperty, value);
   }

        // ── SearchText (TwoWay) ────────────────────────────────────────────
        // propertyChanged syncs Entry from VM (e.g. when VM clears on nav back).
        // The _suppressSync flag prevents Entry.Text ? SetValue ? Entry.Text loop.
        public static readonly BindableProperty SearchTextProperty =
    BindableProperty.Create(
         nameof(SearchText), typeof(string), typeof(VaultPageHero),
         string.Empty, BindingMode.TwoWay,
          propertyChanged: (bindable, _, newValue) =>
          {
    var hero = (VaultPageHero)bindable;
            if (hero._suppressSync) return;
var text = (string?)newValue ?? string.Empty;
    if (hero.SearchEntry.Text != text)
     hero.SearchEntry.Text = text;
                hero.ClearButton.IsVisible = text.Length > 0;
         });
        public string SearchText
        {
       get => (string)GetValue(SearchTextProperty);
          set => SetValue(SearchTextProperty, value);
   }

        // ── SearchCommand – fired after debounce ───────────────────────────
        public static readonly BindableProperty SearchCommandProperty =
        BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(VaultPageHero), null);
    public ICommand SearchCommand
    {
get => (ICommand)GetValue(SearchCommandProperty);
    set => SetValue(SearchCommandProperty, value);
  }

        // ── Internal state ─────────────────────────────────────────────────
        private bool _isSearchOpen;
 private bool _suppressSync;   // re-entrancy guard
        private CancellationTokenSource? _cts;  // debounce token

     public VaultPageHero()
        {
    InitializeComponent();
        }

        // ── Open ───────────────────────────────────────────────────────────
        private async void OnSearchButtonClicked(object sender, EventArgs e)
        {
     if (_isSearchOpen) return;
            _isSearchOpen = true;

            // Start hidden above the title row
     SearchRow.TranslationY = -12;
            SearchRow.Opacity = 0;
          SearchRow.IsVisible = true;

            await Task.WhenAll(
    TitleRow.FadeTo(0, 150, Easing.CubicIn),
         SearchRow.FadeTo(1, 180, Easing.CubicOut),
     SearchRow.TranslateTo(0, 0, 180, Easing.CubicOut));

            TitleRow.IsVisible = false;
            SearchEntry.Focus();
        }

        // ── Close ──────────────────────────────────────────────────────────
   private async void OnCloseSearchClicked(object sender, EventArgs e)
        {
if (!_isSearchOpen) return;
            _isSearchOpen = false;

            SearchEntry.Unfocus();
            SetSilently(string.Empty);   // clear text + fire command

        TitleRow.IsVisible = true;
          TitleRow.Opacity = 0;

     await Task.WhenAll(
                SearchRow.FadeTo(0, 150, Easing.CubicIn),
          SearchRow.TranslateTo(0, -12, 150, Easing.CubicIn),
        TitleRow.FadeTo(1, 200, Easing.CubicOut));

    SearchRow.IsVisible = false;
 SearchRow.TranslationY = -12;
        }

        // ── Clear (keep search open) ───────────────────────────────────────
        private void OnClearSearchClicked(object sender, EventArgs e)
     {
            SetSilently(string.Empty);
            ClearButton.IsVisible = false;
            SearchEntry.Focus();
        }

        // ── Every keystroke – debounced 150 ms ────────────────────────────
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
      var text = e.NewTextValue ?? string.Empty;

            // Sync the bindable property without re-entering this handler
            _suppressSync = true;
            try { SearchText = text; }
            finally { _suppressSync = false; }

     ClearButton.IsVisible = text.Length > 0;

     // Cancel previous pending search, start fresh timer
     _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
var token = _cts.Token;

            // Wait 150 ms of typing silence before executing the search
     Task.Delay(150, token).ContinueWith(t =>
      {
           if (t.IsCanceled) return;
          MainThread.BeginInvokeOnMainThread(() =>
       {
// Double-check text is still current before executing
    if (!token.IsCancellationRequested)
           SearchCommand?.Execute(SearchText);
         });
 }, TaskScheduler.Default);
        }

        // ── Sync text + fire command without re-entrancy ───────────────────
        private void SetSilently(string text)
        {
      _cts?.Cancel();     // kill any pending debounced search
 _suppressSync = true;
   try
            {
     if (SearchEntry.Text != text) SearchEntry.Text = text;
                SearchText = text;
     SearchCommand?.Execute(text);   // immediate on clear
   }
            finally { _suppressSync = false; }
 }
    }
}

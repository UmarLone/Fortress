using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SearchableTitleView : ContentView
    {
        private bool _isSearchVisible = false;

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(SearchableTitleView), "Title", 
                propertyChanged: OnTitleChanged);

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchableTitleView), "Search...",
                propertyChanged: OnPlaceholderChanged);

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SearchableTitleView view && newValue is string title)
            {
                view.TitleLabel.Text = title;
            }
        }

        private static void OnPlaceholderChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SearchableTitleView view && newValue is string placeholder)
            {
                view.SearchEntry.Placeholder = placeholder;
            }
        }

        public event EventHandler<string>? SearchTextChanged;

        public SearchableTitleView()
        {
            InitializeComponent();
            TitleLabel.Text = Title;
            SearchEntry.Placeholder = Placeholder;
        }

        void OnSearchEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(e.NewTextValue);
            ClearButton.IsVisible = hasText;
            SearchTextChanged?.Invoke(this, e.NewTextValue ?? string.Empty);
        }

        void OnClearClicked(object sender, EventArgs e)
        {
            SearchEntry.Text = string.Empty;
            SearchEntry.Focus();
        }

        void OnSearchToggleClicked(object sender, EventArgs e)
        {
            _isSearchVisible = !_isSearchVisible;

            TitleLabel.IsVisible = !_isSearchVisible;
            SearchContainer.IsVisible = _isSearchVisible;

            if (_isSearchVisible)
            {
                ToggleIcon.Icon(MaterialIcons.Close);
                SearchEntry.Focus();
            }
            else
            {
                ToggleIcon.Icon(MaterialIcons.Search);
                SearchEntry.Text = string.Empty;
                SearchEntry.Unfocus();
                ClearButton.IsVisible = false;
            }
        }

        public void Reset()
        {
            if (_isSearchVisible)
            {
                _isSearchVisible = false;
                TitleLabel.IsVisible = true;
                SearchContainer.IsVisible = false;
                SearchEntry.Text = string.Empty;
                ToggleIcon.Icon(MaterialIcons.Search);
                ClearButton.IsVisible = false;
            }
        }
    }
}

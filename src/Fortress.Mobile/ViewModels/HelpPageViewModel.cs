using Fortress.Mobile.Core.Contracts;
using Fortress.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Fortress.ViewModels
{
    public class HelpPageViewModel : ViewModelBase
    {
        private readonly ILogger<HelpPageViewModel> _logger;

        // ── search ───────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplySearch(value);
            }
        }

        // ── data ─────────────────────────────────────────────────────────────
        private List<HelpCategory> _allCategories = new();

        /// <summary>
        /// Pre-flattened rows: CategoryHeader → N×Question → …repeat → Footer.
        /// Drives a virtualised CollectionView so off-screen items are not inflated.
        /// </summary>
        private ObservableCollection<HelpRow> _rows = new();
        public ObservableCollection<HelpRow> Rows
        {
            get => _rows;
            private set => SetProperty(ref _rows, value);
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => SetProperty(ref _isEmpty, value);
        }

        // ── version / copyright (kept for hero subtitle) ─────────────────────
        public string CopyrightText => $"© {DateTime.Now.Year} Fortress Password Manager";

        private string _version = string.Empty;
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        // ── constructor ───────────────────────────────────────────────────────
        public HelpPageViewModel(INavigationService navigationService,
       ILogger<HelpPageViewModel> logger,
     IAppInfo appInfo)
        : base(navigationService)
        {
            _logger = logger;
            Version = appInfo.VersionString;
        }

        // ── navigation ────────────────────────────────────────────────────────
        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await LoadHelpContentAsync();
        }

        // ── load ──────────────────────────────────────────────────────────────
        private async Task LoadHelpContentAsync()
        {
            IsLoading = true;
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("help_content.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                // Explicitly read as UTF-8 to guard against any BOM or encoding issues
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
                var json = await reader.ReadToEndAsync();
                var categories = JsonSerializer.Deserialize<List<HelpCategory>>(json, options)
                    ?? new List<HelpCategory>();

                _allCategories = categories;
                PublishRows(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load help content");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── flatten helper ────────────────────────────────────────────────────
        /// <summary>Converts the category tree into the flat row list the CollectionView consumes.</summary>
        private void PublishRows(IEnumerable<HelpCategory> categories)
        {
            var rows = new List<HelpRow>();
            foreach (var cat in categories)
            {
                rows.Add(new HelpRow
                {
                    Kind = HelpRowKind.CategoryHeader,
                    CategoryTitle = cat.Category,
                    CategoryIcon = cat.IconGlyph,
                });
                foreach (var item in cat.Items)
                    rows.Add(new HelpRow { Kind = HelpRowKind.Question, Item = item });
            }
            rows.Add(new HelpRow { Kind = HelpRowKind.Footer });

            Rows = new ObservableCollection<HelpRow>(rows);
            IsEmpty = rows.Count <= 1; // only footer = nothing
        }

        // ── search ────────────────────────────────────────────────────────────
        private void ApplySearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Restore all and collapse everything
                foreach (var cat in _allCategories)
                    foreach (var item in cat.Items)
                        item.IsExpanded = false;

                PublishRows(_allCategories);
                return;
            }

            var q = query.Trim();
            var filtered = _allCategories
                .Select(cat => new HelpCategory
                {
                    Category = cat.Category,
                    IconGlyph = cat.IconGlyph,
                    Items = cat.Items
                        .Where(i => i.Question.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || i.Answer.Contains(q, StringComparison.OrdinalIgnoreCase))
                        .Select(i => { i.IsExpanded = true; return i; })
                        .ToList()
                })
                .Where(cat => cat.Items.Count > 0)
                .ToList();

            PublishRows(filtered);
        }

        // ── accordion toggle ──────────────────────────────────────────────────
        private DelegateCommand<HelpItem>? _toggleItemCommand;
        public DelegateCommand<HelpItem> ToggleItemCommand =>
            _toggleItemCommand ??= new DelegateCommand<HelpItem>(item =>
            {
                if (item is null) return;
                item.IsExpanded = !item.IsExpanded;
            });

        // ── clear search ──────────────────────────────────────────────────────
        private DelegateCommand? _clearSearchCommand;
        public DelegateCommand ClearSearchCommand =>
            _clearSearchCommand ??= new DelegateCommand(() => SearchText = string.Empty);

        // ── back ──────────────────────────────────────────────────────────────
        private DelegateCommand? _goBackCommand;
        public DelegateCommand GoBackCommand =>
 _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
    }
}

using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    /// <summary>
    /// Arguments passed to <see cref="AddMemberSheetViewModel.InitializeAsync"/>.
    /// </summary>
    public sealed record AddMemberSheetArgs(
        IReadOnlyList<CredentialView> Available,
        Func<CredentialView, Task> OnPicked);

    /// <summary>
    /// ViewModel for the searchable "add member" bottom sheet.
    ///
    /// Key design decisions that prevent UI freezes with large vaults:
    ///  1. Filtering runs entirely on the thread pool (<see cref="Task.Run"/>).
    ///  2. The visible list is capped at <see cref="PageSize"/> items at all times.
    ///  3. The XAML uses a virtualised <see cref="CollectionView"/>, NOT BindableLayout.
    ///  4. A 250 ms debounce on <see cref="SearchText"/> avoids per-keystroke rebuilds.
  ///  5. The sheet uses a <c>FullscreenDetent</c> so Android never measures every row
    ///     up-front just to size the sheet.
    /// </summary>
    public class AddMemberSheetViewModel : BottomSheetViewModelBase
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private const int PageSize = 50;
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);

        // ── Private state ────────────────────────────────────────────────────
        private IReadOnlyList<CredentialView> _all = [];
        private Func<CredentialView, Task>? _onPicked;
    private CancellationTokenSource? _debounceCts;

      // ── Properties ───────────────────────────────────────────────────────
        private string _title = "Add to group";
     public string Title
 {
      get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
   set
     {
            if (SetProperty(ref _searchText, value))
            _ = DebounceFilterAsync(value);
            }
        }

        private ObservableCollection<CredentialView> _visible = [];
        public ObservableCollection<CredentialView> Visible
        {
  get => _visible;
            private set => SetProperty(ref _visible, value);
   }

        private int _totalAvailable;
        public int TotalAvailable
        {
       get => _totalAvailable;
            private set
            {
      SetProperty(ref _totalAvailable, value);
         RaisePropertyChanged(nameof(IsCapped));
        RaisePropertyChanged(nameof(CappedLabel));
      }
    }

 private bool _isFiltering;
      public bool IsFiltering
        {
   get => _isFiltering;
      private set => SetProperty(ref _isFiltering, value);
 }

        private bool _isEmpty;
      public bool IsEmpty
        {
            get => _isEmpty;
 private set => SetProperty(ref _isEmpty, value);
        }

      /// <summary>True when the full result set is larger than <see cref="PageSize"/>.</summary>
        public bool IsCapped => TotalAvailable > PageSize;

        /// <summary>e.g. "Showing 50 of 1,240 – type to narrow"</summary>
 public string CappedLabel =>
      $"Showing {Math.Min(TotalAvailable, PageSize):N0} of {TotalAvailable:N0} – type to narrow results";

        // ── Init ─────────────────────────────────────────────────────────────
      public override async Task InitializeAsync(object args, string title = null)
        {
       if (args is not AddMemberSheetArgs sheet) return;

            Title = title ?? "Add to group";
   _all = sheet.Available;
            _onPicked = sheet.OnPicked;
     TotalAvailable = _all.Count;

  await ApplyFilterAsync(string.Empty);
        }

        // ── Filtering ─────────────────────────────────────────────────────────
        /// <summary>Debounced entry point – cancels in-flight work before scheduling new.</summary>
        private async Task DebounceFilterAsync(string text)
        {
  _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
         var token = _debounceCts.Token;

            try
            {
  await Task.Delay(DebounceDelay, token);
              if (!token.IsCancellationRequested)
       await ApplyFilterAsync(text);
            }
            catch (OperationCanceledException) { /* superseded – ignore */ }
   }

        private async Task ApplyFilterAsync(string text)
        {
      IsFiltering = true;
            try
        {
           var term = text?.Trim() ?? string.Empty;

    // All filtering on the thread pool – never block the UI thread
    var results = await Task.Run(() =>
 {
       IEnumerable<CredentialView> source = _all;

      if (!string.IsNullOrEmpty(term))
source = source.Where(c =>
  (c.Domain?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
     (c.Username?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

   return source.Take(PageSize).ToList();
     });

   var filteredTotal = await Task.Run(() =>
     {
          if (string.IsNullOrEmpty(term)) return _all.Count;
       return _all.Count(c =>
  (c.Domain?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
         (c.Username?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
     });

      // Marshal back to the UI thread for collection assignment
                await MainThread.InvokeOnMainThreadAsync(() =>
          {
                  TotalAvailable = filteredTotal;
        Visible = new ObservableCollection<CredentialView>(results);
        IsEmpty = results.Count == 0;
                });
       }
       finally
            {
    IsFiltering = false;
      }
      }

        // ── Commands ─────────────────────────────────────────────────────────
  private AsyncCommand<CredentialView>? _pickCommand;
 public ICommand PickCommand =>
         _pickCommand ??= new AsyncCommand<CredentialView>(async item =>
        {
   if (item is null) return;
         DismissAction?.Invoke();
     if (_onPicked is not null)
               await _onPicked(item);
    });

      private DelegateCommand? _clearSearchCommand;
        public DelegateCommand ClearSearchCommand =>
 _clearSearchCommand ??= new DelegateCommand(() => SearchText = string.Empty);
    }
}

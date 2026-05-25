using Android.App;
using Android.Content;
using Android.OS;
using Android.Text.Method;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView.Widget;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Mobile.Platforms.Android;
using Fortress.Droid.Renderers;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.TextField;
using Newtonsoft.Json;
using ImageButton = Android.Widget.ImageButton;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;

namespace Bit.Droid.Autofill
{
    public class AutofillConstants
    {
        public const string AutofillFramework = "autofillFramework";
        public const string AutofillFrameworkFillType = "autofillFrameworkFillType";
        public const string AutofillFrameworkUri = "autofillFrameworkUri";
        public const string AutofillFrameworkCipherId = "autofillFrameworkCipherId";
        public const string AppPackageName = "com.fortress.app";
    }

    public class CredentialsAdapter : RecyclerView.Adapter
    {
        private readonly Action<CredentialView> _onClick;
        private readonly Action<CredentialView> _onOptions;
        private List<CredentialView> _allItems;   // source truth for Filter()
        private readonly IList<CredentialView> _items;  // currently displayed

        public CredentialsAdapter(
            IList<CredentialView> items,
            Action<CredentialView> onClick,
            Action<CredentialView> onOptions)
        {
            _allItems = items.ToList();
            _items = items;
            _onClick = onClick;
            _onOptions = onOptions;
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                .Inflate(Resource.Layout.row_credential, parent, false);

            return new CredentialViewHolder(view, _onClick, _onOptions);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = (CredentialViewHolder)holder;
            vh.Bind(_items[position]);
        }
        /// <summary>Replace the displayed list with <paramref name="items"/> (already filtered externally).</summary>
        public void SwapItems(IEnumerable<CredentialView> items)
        {
            _items.Clear();
            foreach (var item in items) _items.Add(item);
            NotifyDataSetChanged();
        }

        /// <summary>
        /// Updates the adapter's source-of-truth list used by <see cref="Filter"/>.
        /// Call this whenever the underlying full list changes (e.g. All tab loaded).
        /// </summary>
        public void UpdateSourceItems(IEnumerable<CredentialView> source)
        {
            _allItems = source.ToList();
        }

        public void Filter(string query)
        {
            _items.Clear();
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var item in _allItems) _items.Add(item);
            }
            else
            {
                var q = query.Trim();
                foreach (var item in _allItems)
                {
                    if ((item.Domain?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.Username?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                        _items.Add(item);
                }
            }
            NotifyDataSetChanged();
        }
    }

    // ── Tab enum ────────────────────────────────────────────────────────────────
    public enum AutofillTab { Matches, All }

    public class AutofillBottomSheetFragment : BottomSheetDialogFragment
    {
        // ── State ──────────────────────────────────────────────────────────────
        private readonly IList<CredentialView> _matchCredentials;   // domain-matched, passed in
        private IList<CredentialView>           _allCredentials;    // loaded lazily on first "All" tap
        private CredentialsAdapter  _adapter;
  private readonly RequestingApplication  _request;
  private AutofillTab           _activeTab = AutofillTab.Matches;
   private bool      _allLoaded;

      /// <summary>
   /// Set to true by the activity before calling FinishAndRemoveTask() so that
      /// OnDismiss does NOT call Activity.Finish() and race with the Result.Ok reply.
    /// </summary>
     public bool FillCommitted { get; set; }

        /// <summary>
        /// Exposes the lazily-loaded "All" credential list so the Activity's
 /// OTP timer can tick it alongside the matched list.
        /// Returns null until the All tab has been loaded.
    /// </summary>
        public IList<CredentialView>? AllCredentials => _allLoaded ? _allCredentials : null;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<CredentialView> OnSelected;
   public event EventHandler    OnAddNew;
     public event EventHandler<BrowseCategory> OnLoadAll;
/// <summary>Fired when the user chooses "Block this site" from the options menu.</summary>
        public event EventHandler<string> OnBlockSite;

        // ── Views ─────────────────────────────────────────────────────────────
        public  EditText      _searchEditText;
      private TextInputLayout     _searchContainer;
      private TextView            _title;
        private TextView            _subtitle;
        private ImageButton         _searchButton;
   private ImageButton         _addButton;
private TextView            _tabMatches;
      private TextView         _tabAll;
        private View       _tabIndicator;
        private RecyclerView        _recycler;
        private LinearLayout  _emptyView;
 private TextView   _emptyTitle;    // headline in the empty state card
    private TextView            _emptyMessage;  // sub-message in the empty state card
private Android.Widget.ProgressBar _loadingSpinner;
      private bool       _isSearchActive;

        public AutofillBottomSheetFragment(
      IList<CredentialView> credentials,
            RequestingApplication request)
        {
            _matchCredentials = credentials;
            _request        = request;
        }

        // ── Sheet sizing ───────────────────────────────────────────────────────

        public override void OnStart()
        {
         base.OnStart();
      if (Dialog is not BottomSheetDialog dialog) return;
        var bottomSheet = dialog.FindViewById<FrameLayout>(Resource.Id.design_bottom_sheet);
     if (bottomSheet == null) return;

   // ── Build a shaped background that matches the header gradient ─────
     // The default MaterialShapeDrawable is white with 20dp rounded top
   // corners. We replace it with one that uses the header gradient's
 // darkest color (#2B64A3) so the rounded corners are blue — not white.
       var shapeModel = new Google.Android.Material.Shape.ShapeAppearanceModel.Builder()
           .SetTopLeftCorner(Google.Android.Material.Shape.CornerFamily.Rounded, 20 * Resources.DisplayMetrics.Density)
       .SetTopRightCorner(Google.Android.Material.Shape.CornerFamily.Rounded, 20 * Resources.DisplayMetrics.Density)
         .SetBottomLeftCorner(Google.Android.Material.Shape.CornerFamily.Rounded, 0)
 .SetBottomRightCorner(Google.Android.Material.Shape.CornerFamily.Rounded, 0)
       .Build();

            var shapeDrawable = new Google.Android.Material.Shape.MaterialShapeDrawable(shapeModel);
   shapeDrawable.FillColor = Android.Content.Res.ColorStateList.ValueOf(
       Android.Graphics.Color.ParseColor("#2B64A3"));

      bottomSheet.Background = shapeDrawable;

       // Force the sheet to a fixed 65% height so it never collapses
      // when the list is empty or the keyboard pushes content up.
            int targetHeight = (int)(Resources.DisplayMetrics.HeightPixels * 0.65);
            bottomSheet.LayoutParameters.Height = targetHeight;
    bottomSheet.RequestLayout();

 var behavior = BottomSheetBehavior.From(bottomSheet);
            behavior.PeekHeight = targetHeight;
          behavior.State = BottomSheetBehavior.StateExpanded;
            behavior.SkipCollapsed = true;
            behavior.Draggable = true;
        }

    public override Dialog OnCreateDialog(Bundle? savedInstanceState)
        {
        var dialog = new BottomSheetDialog(Context!, Resource.Style.BottomSheetDialogTheme);
          // Prevent the dialog being cancelled by tapping outside — a touch on the
            // RecyclerView row can register as a touch-outside on some Android versions,
            // firing OnDismiss → Activity.Finish() before TriggerAutofill's awaits complete.
            dialog.SetCancelable(false);
        dialog.SetCanceledOnTouchOutside(false);
          return dialog;
        }

        // ── View creation ──────────────────────────────────────────────────────

        public override View OnCreateView(
  LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
        var view = inflater.Inflate(Resource.Layout.bs_autofill, container, false);

            // Header
       _title = view.FindViewById<TextView>(Resource.Id.title);
      _subtitle       = view.FindViewById<TextView>(Resource.Id.subtitle);

    // Apply Audiowide font — same typeface used in the MAUI XAML pages
    if (_title != null)
       {
         _title.Typeface = FontHelper.Audiowide(RequireContext());
                _title.LetterSpacing = 0.15f;   // ~3 in MAUI CharacterSpacing units
            }

          _searchEditText  = view.FindViewById<EditText>(Resource.Id.searchEditText);
            _searchContainer = view.FindViewById<TextInputLayout>(Resource.Id.searchContainer);
            _searchButton    = view.FindViewById<ImageButton>(Resource.Id.searchButton);
    _addButton       = view.FindViewById<ImageButton>(Resource.Id.addButton);
          // Tabs
   _tabMatches   = view.FindViewById<TextView>(Resource.Id.tabMatches);
            _tabAll       = view.FindViewById<TextView>(Resource.Id.tabAll);
   _tabIndicator = view.FindViewById<View>(Resource.Id.tabIndicator);

          // Content
 _recycler       = view.FindViewById<RecyclerView>(Resource.Id.credentialsList);
        _emptyView      = view.FindViewById<LinearLayout>(Resource.Id.emptyView);
       _emptyTitle     = view.FindViewById<TextView>(Resource.Id.emptyTitle);
          _emptyMessage   = view.FindViewById<TextView>(Resource.Id.emptyMessage);
  _loadingSpinner = view.FindViewById<Android.Widget.ProgressBar>(Resource.Id.loadingSpinner);

         // Subtitle
     var appName = _request?.Name;
         _subtitle.Text = string.IsNullOrWhiteSpace(appName)
         ? "Tap a credential to fill"
   : $"Filling for {appName}";

         // Adapter — starts with match list
    _recycler.SetLayoutManager(new LinearLayoutManager(Context));
 var displayList = new List<CredentialView>(_matchCredentials ?? new List<CredentialView>());
     _adapter = new CredentialsAdapter(displayList, OnCredentialSelected, OnOptionsSelected);
  _recycler.SetAdapter(_adapter);

   // Initial Matches tab state
            SelectTab(AutofillTab.Matches, animate: false);

    // ── Tab clicks ──────────────────────────────────────────────────────
  _tabMatches.Click += (_, __) => SelectTab(AutofillTab.Matches);
            _tabAll.Click     += (_, __) => SelectTab(AutofillTab.All);

            // ── Add button ──────────────────────────────────────────────────────
     _addButton.Click += (_, __) => { OnAddNew?.Invoke(this, EventArgs.Empty); Dismiss(); };

            // ── Search toggle ────────────────────────────────────────────────────
        _searchButton.Click += (_, __) =>
   {
            if (_isSearchActive) CollapseSearch(); else ExpandSearch();
   };

        _searchEditText.TextChanged += (_, __) =>
      {
         if (!_isSearchActive) return;
            var query = _searchEditText.Text ?? string.Empty;
          if (string.IsNullOrEmpty(query))
            {
     // Restore the correct source list for the active tab
     RestoreActiveTabItems();
     }
            else
          {
              _adapter.Filter(query);
       // Show "no items found for X" if filter returned nothing
      UpdateVisibility(loading: false, empty: _adapter.ItemCount == 0, searchQuery: query);
            }
        };

    _searchEditText.FocusChange += (_, e) =>
        {
            if (!e.HasFocus && string.IsNullOrEmpty(_searchEditText.Text)) CollapseSearch();
        };

            return view;
      }

        // ── Tab selection ──────────────────────────────────────────────────────

 private void SelectTab(AutofillTab tab, bool animate = true)
        {
 if (_isSearchActive) CollapseSearch();
      _activeTab = tab;

if (tab == AutofillTab.Matches)
            {
           _tabMatches.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
      _tabMatches.SetTextColor(Android.Graphics.Color.ParseColor("#407cca"));
  _tabAll.SetTypeface(null, Android.Graphics.TypefaceStyle.Normal);
    _tabAll.SetTextColor(Android.Graphics.Color.ParseColor("#9CA3AF"));

    // Slide indicator to left (Matches) side — TranslationX = 0
      if (animate)
                    _tabIndicator.Animate().TranslationX(0f).SetDuration(180).Start();
      else
      _tabIndicator.TranslationX = 0f;

        ShowMatchesContent();
         }
            else
 {
      _tabAll.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
          _tabAll.SetTextColor(Android.Graphics.Color.ParseColor("#407cca"));
   _tabMatches.SetTypeface(null, Android.Graphics.TypefaceStyle.Normal);
      _tabMatches.SetTextColor(Android.Graphics.Color.ParseColor("#9CA3AF"));

 // Slide indicator to right (All) side.
          // The indicator is already half the strip width in XML, so
// we just translate it by its own measured width to sit under "All".
     _tabIndicator.Post(() =>
     {
           float shift = _tabIndicator.Width > 0
        ? _tabIndicator.Width       // exactly half the strip
        : _tabIndicator.RootView.Width / 2f; // fallback before measure

         if (animate)
            _tabIndicator.Animate().TranslationX(shift).SetDuration(180).Start();
            else
          _tabIndicator.TranslationX = shift;
 });

 ShowAllContent();
    }
        }


 private void ShowMatchesContent()
    {
     var items = _matchCredentials ?? new List<CredentialView>();
 _adapter.SwapItems(items);
      UpdateVisibility(loading: false, empty: !items.Any());
        }

        private void ShowAllContent()
        {
    if (_allLoaded && _allCredentials != null)
 {
        // Already fetched — just display
        _adapter.SwapItems(_allCredentials);
      UpdateVisibility(loading: false, empty: !_allCredentials.Any());
      return;
         }

   // Show spinner and ask the Activity to load the full list
      UpdateVisibility(loading: true, empty: false);
     OnLoadAll?.Invoke(this, GetBrowseCategory());
        }

        /// <summary>
    /// Called by the Activity when the full "All" list has been loaded.
        /// Must be called on the UI thread.
        /// </summary>
        public void DeliverAllCredentials(IList<CredentialView> all)
     {
            _allCredentials = all;
_allLoaded = true;

   // Keep the adapter's filter source in sync with the full all-credentials list
       _adapter?.UpdateSourceItems(all);

    if (_activeTab == AutofillTab.All)
   {
    // If search is active, re-apply the current query against the new source
      if (_isSearchActive && !string.IsNullOrEmpty(_searchEditText?.Text))
    {
       var q = _searchEditText.Text;
       _adapter.Filter(q);
              UpdateVisibility(loading: false, empty: _adapter.ItemCount == 0, searchQuery: q);
    }
   else
       {
           _adapter.SwapItems(_allCredentials);
       UpdateVisibility(loading: false, empty: !_allCredentials.Any());
         }
    }
      }

    private void UpdateVisibility(bool loading, bool empty, string? searchQuery = null)
        {
        _loadingSpinner.Visibility = loading ? ViewStates.Visible : ViewStates.Gone;
          _recycler.Visibility       = (!loading && !empty) ? ViewStates.Visible : ViewStates.Gone;

      if (!loading && empty)
          {
     _emptyView.Visibility = ViewStates.Visible;

      // Show search-specific message when a query is active
       if (!string.IsNullOrEmpty(searchQuery))
   {
_emptyTitle.Text   = "No items found";
      _emptyMessage.Text = $"No results for \"{searchQuery}\".\nTry a different search term.";
     }
     else
  {
      // Default — no items in this tab at all
         _emptyTitle.Text   = "No matches found";
     _emptyMessage.Text = "Tap \"All\" above to browse all saved entries.";
        }
           }
 else
    {
 _emptyView.Visibility = ViewStates.Gone;
     }
        }

        /// <summary>Maps the fill type of the requesting app to the correct browse category.</summary>
     private BrowseCategory GetBrowseCategory()
   {
         return _request?.FillRequestType switch
       {
     FillRequestType.Card     => BrowseCategory.Cards,
         FillRequestType.Identity => BrowseCategory.Identities,
            _ => BrowseCategory.Logins,
       };
    }

     // ── Search expand / collapse ───────────────────────────────────────────

 private void ExpandSearch()
        {
            if (_isSearchActive) return;
          _isSearchActive = true;
            _searchButton.SetImageResource(Resource.Drawable.ic_close);

      // Only hide the subtitle pill to save a little vertical space.
  // Logo + FORTRESS title stay visible for branding continuity.
      _subtitle.Visibility = ViewStates.Gone;

     _searchContainer.Alpha = 0f;
       _searchContainer.Visibility = ViewStates.Visible;
       _searchContainer.Animate().Alpha(1f).SetDuration(160).Start();
     _searchEditText.RequestFocus();
            var imm = (InputMethodManager)Context.GetSystemService(Context.InputMethodService);
  imm?.ShowSoftInput(_searchEditText, ShowFlags.Implicit);
    }

        public void CollapseSearch()
        {
            if (!_isSearchActive) return;
            _isSearchActive = false;
         _searchButton.SetImageResource(Resource.Drawable.ic_search);
 _searchEditText.Text = string.Empty;

 RestoreActiveTabItems();

            _searchContainer.Animate().Alpha(0f).SetDuration(130)
       .WithEndAction(new Java.Lang.Runnable(() =>
       {
     _searchContainer.Visibility = ViewStates.Gone;
  // Restore subtitle pill
_subtitle.Visibility = ViewStates.Visible;
              }))
         .Start();
    }

        /// <summary>
        /// Restores the RecyclerView to the full unfiltered list for the active tab.
        /// Called when the user clears the search field or collapses the search bar.
        /// </summary>
   private void RestoreActiveTabItems()
        {
         if (_activeTab == AutofillTab.Matches)
      {
var items = _matchCredentials ?? new List<CredentialView>();
        _adapter.SwapItems(items);
  UpdateVisibility(loading: false, empty: !items.Any());
            }
            else
            {
     if (_allLoaded && _allCredentials != null)
      {
        _adapter.SwapItems(_allCredentials);
   UpdateVisibility(loading: false, empty: !_allCredentials.Any());
   }
      else
          {
            // All tab not yet loaded — trigger load again
     UpdateVisibility(loading: true, empty: false);
       OnLoadAll?.Invoke(this, GetBrowseCategory());
       }
    }
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        public override void OnCreate(Bundle savedInstanceState)
  {
            base.OnCreate(savedInstanceState);
      Dialog?.SetOnKeyListener(new BackKeyListener(this));
   }

        public override void OnDismiss(IDialogInterface dialog)
        {
            base.OnDismiss(dialog);
         // Only finish the activity on genuine user dismissal (swipe down / back).
     // When a fill was committed the activity sets FillCommitted = true before
            // calling FinishAndRemoveTask(), so we skip the redundant Finish() here.
            if (!FillCommitted)
   Activity?.Finish();
      }

        public void NotifyCredentialsChanged() => _adapter?.NotifyDataSetChanged();

 // ── Actions ────────────────────────────────────────────────────────────

        private void OnCredentialSelected(CredentialView credential)
        {
            OnSelected?.Invoke(this, credential);
      // Do NOT call Dismiss() — TriggerAutofill is async and FinishAndRemoveTask()
      // will tear down this fragment naturally when the fill is committed.
        }

        private async void OnOptionsSelected(CredentialView credential)
        {
         var svc = Shiny.Hosting.Host.ServiceProvider.GetService<IDeviceServices>();
            var options = new List<BottomSheetOption>();
      var blockUri = _request?.Package;

       var credType = credential.CredentialType ?? string.Empty;

// ── CREDIT CARD ───────────────────────────────────────────────────
            if (credType == "CreditCard" || credType == "CreditCard")
      {
            CardAutofillMeta? meta = null;
                if (!string.IsNullOrEmpty(credential.Meta))
     try { meta = JsonConvert.DeserializeObject<CardAutofillMeta>(credential.Meta); } catch { }

    if (!string.IsNullOrEmpty(meta?.CardholderName))
          options.Add(new() {
   Title = "Copy Name",
       IconResId = Resource.Drawable.ic_copy,
           Action = async () => await svc.CopyToClipboard(
        meta.CardholderName, "Name copied",
      PreferenceWrapper.Instance.ClearClipboardTimeout)
         });

      if (!string.IsNullOrEmpty(meta?.Number))
            options.Add(new() {
        Title = "Copy Card Number",
           IconResId = Resource.Drawable.ic_copy,
  Action = async () => await svc.CopyToClipboard(
         meta.Number, "Card number copied",
 PreferenceWrapper.Instance.ClearClipboardTimeout)
  });

      if (!string.IsNullOrEmpty(meta?.Cvv))
           options.Add(new() {
    Title = "Copy CVV",
       IconResId = Resource.Drawable.ic_copy,
      Action = async () => await svc.CopyToClipboard(
      meta.Cvv, "CVV copied",
         PreferenceWrapper.Instance.ClearClipboardTimeout)
               });

                var expiry = BuildExpiryString(meta?.ExpMonth, meta?.ExpYear);
          if (!string.IsNullOrEmpty(expiry))
      options.Add(new() {
     Title = "Copy Expiry",
    IconResId = Resource.Drawable.ic_copy,
             Action = async () => await svc.CopyToClipboard(
    expiry, "Expiry copied",
      PreferenceWrapper.Instance.ClearClipboardTimeout)
          });
     }
    // ── IDENTITY / ADDRESS ────────────────────────────────────────────
  else if (credType == "Address" || credType == "Identity")
            {
            IdentityAutofillMeta? meta = null;
      if (!string.IsNullOrEmpty(credential.Meta))
      try { meta = JsonConvert.DeserializeObject<IdentityAutofillMeta>(credential.Meta); } catch { }

      var fullName = $"{meta?.FirstName} {meta?.LastName}".Trim();
   if (!string.IsNullOrEmpty(fullName))
              options.Add(new() {
               Title = "Copy Name",
IconResId = Resource.Drawable.ic_copy,
      Action = async () => await svc.CopyToClipboard(
                   fullName, "Name copied",
                 PreferenceWrapper.Instance.ClearClipboardTimeout)
        });

                if (!string.IsNullOrEmpty(meta?.Email))
            options.Add(new() {
      Title = "Copy Email",
        IconResId = Resource.Drawable.ic_copy,
           Action = async () => await svc.CopyToClipboard(
   meta.Email, "Email copied",
     PreferenceWrapper.Instance.ClearClipboardTimeout)
       });

        if (!string.IsNullOrEmpty(meta?.Phone))
          options.Add(new() {
                 Title = "Copy Phone",
         IconResId = Resource.Drawable.ic_copy,
      Action = async () => await svc.CopyToClipboard(
             meta.Phone, "Phone copied",
 PreferenceWrapper.Instance.ClearClipboardTimeout)
  });

   if (!string.IsNullOrEmpty(meta?.Address))
  options.Add(new() {
                 Title = "Copy Address",
    IconResId = Resource.Drawable.ic_copy,
       Action = async () => await svc.CopyToClipboard(
       meta.Address, "Address copied",
         PreferenceWrapper.Instance.ClearClipboardTimeout)
     });

      if (!string.IsNullOrEmpty(meta?.City))
         options.Add(new() {
           Title = "Copy City",
     IconResId = Resource.Drawable.ic_copy,
   Action = async () => await svc.CopyToClipboard(
       meta.City, "City copied",
       PreferenceWrapper.Instance.ClearClipboardTimeout)
          });

     if (!string.IsNullOrEmpty(meta?.PostalCode))
         options.Add(new() {
      Title = "Copy Postal Code",
   IconResId = Resource.Drawable.ic_copy,
   Action = async () => await svc.CopyToClipboard(
                   meta.PostalCode, "Postal code copied",
              PreferenceWrapper.Instance.ClearClipboardTimeout)
   });
        }
          // ── LOGIN ─────────────────────────────────────────────────────────
          else
       {
       if (!string.IsNullOrEmpty(credential.Username))
  options.Add(new() {
    Title = "Copy Username",
       IconResId = Resource.Drawable.ic_copy,
     Action = async () => await svc.CopyToClipboard(
  credential.Username, "Username copied",
        PreferenceWrapper.Instance.ClearClipboardTimeout)
    });

           if (!string.IsNullOrEmpty(credential.Password))
           options.Add(new() {
        Title = "Copy Password",
               IconResId = Resource.Drawable.ic_copy,
            Action = async () => await svc.CopyToClipboard(
       credential.Password, "Password copied",
         PreferenceWrapper.Instance.ClearClipboardTimeout)
          });

          if (credential.HasOtp)
    options.Add(new() {
   Title = "Copy One-Time Passcode",
    IconResId = Resource.Drawable.ic_copy,
      Action = async () =>
                 {
      var otp = OtpHelper.GenerateOtp(credential.Data).Code;
       if (string.IsNullOrEmpty(otp)) { svc.Toast("Invalid OTP secret"); return; }
     await svc.CopyToClipboard(otp, "OTP copied",
  PreferenceWrapper.Instance.ClearClipboardTimeout);
         }
      });
        }

    // ── FILL NOW — always present ─────────────────────────────────────
       options.Add(new() {
  Title = "Fill Now",
   IconResId = Resource.Drawable.ic_fill,
     Action = () => { OnCredentialSelected(credential); return Task.CompletedTask; }
     });

   // ── BLOCK — always present when not already blocked ───────────────
  if (!string.IsNullOrWhiteSpace(blockUri) &&
   !PreferenceWrapper.Instance.IsAutofillBlockedFor(blockUri))
   {
  options.Add(new() {
          Title = "Don't fill on this site",
             IconResId = Resource.Drawable.ic_block,
         Action = () =>
        {
  OnBlockSite?.Invoke(this, blockUri);
               Dismiss();
         return Task.CompletedTask;
     }
                });
            }

     new CredentialOptionsBottomSheet(options)
  .Show(((FragmentActivity)Context).SupportFragmentManager, "options");
        }

        /// <summary>Formats month/year into "MM/YY" or "MM/YYYY" for display.</summary>
        private static string BuildExpiryString(string? month, string? year)
        {
    if (string.IsNullOrEmpty(month) && string.IsNullOrEmpty(year)) return string.Empty;
   var displayYear = year?.Length == 4 ? year[2..] : year ?? string.Empty;
            return $"{month ?? string.Empty}/{displayYear}".Trim('/');
        }
    }  // end AutofillBottomSheetFragment

    class BackKeyListener : Java.Lang.Object, IDialogInterfaceOnKeyListener
    {
     private readonly AutofillBottomSheetFragment _fragment;
        public BackKeyListener(AutofillBottomSheetFragment fragment) => _fragment = fragment;

 public bool OnKey(IDialogInterface dialog, Keycode keyCode, KeyEvent e)
        {
    if (keyCode == Keycode.Back && e.Action == KeyEventActions.Up)
            {
        if (_fragment._searchEditText?.Visibility == ViewStates.Visible)
    {
   _fragment.CollapseSearch();
         return true;
        }
       }
            return false;
        }
    }

 // ── Browse-all request DTO ─────────────────────────────────────────────────
    public enum BrowseCategory { Logins, Cards, Identities }
    public sealed class BrowseAllRequest
    {
        public BrowseCategory Category { get; }
        public BrowseAllRequest(BrowseCategory category) => Category = category;
    }
}  // end namespace

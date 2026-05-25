using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Fortress.Services;
using MauiIcons.Core;
using MauiIcons.Material;
namespace Fortress.ViewModels
{
    public class GroupsPageViewModel : ViewModelBase
    {
        #region Properties

        private ObservableCollection<VaultGroupViewModel> _groups = new();
        public ObservableCollection<VaultGroupViewModel> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
        }

        private bool _noGroups;
        public bool NoGroups { get => _noGroups; set => SetProperty(ref _noGroups, value); }

        private bool _isRefreshing;
        public bool IsRefreshing { get => _isRefreshing; set => SetProperty(ref _isRefreshing, value); }

        #endregion

        private readonly IDataStorageService _storage;
        private readonly IBottomSheetService _sheets;
        private readonly ILogger<GroupsPageViewModel> _logger;

        // ── Colour palette ─────────────────────────────────────────────────────
        private static readonly string[] _palette =
        {
        "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6",
      "#EC4899", "#06B6D4", "#84CC16", "#F97316", "#6366F1"
    };
        private int _nextColour;

        // ── Default seed groups ────────────────────────────────────────────────
        // Created once on first launch if the vault has no groups at all.
        private static readonly (string Name, string Icon, string Color)[] _defaultGroups =
          {
      ("Finance",    "AccountBalance", "#10B981"),
            ("Social",  "People",         "#3B82F6"),
            ("Shopping",   "ShoppingCart",   "#F59E0B"),
   ("Work", "Work",    "#6366F1"),
       ("Travel",   "Flight",         "#06B6D4"),
        ("Personal", "Person",         "#EC4899"),
        };

        private const string SeedDonePrefKey = "vault_groups_seeded_v1";
        // Exposed so CredentialsPageViewModel can trigger seeding on first open
        public const string SeedDonePrefKeyPublic = SeedDonePrefKey;

        public GroupsPageViewModel(
     INavigationService navigationService,
        IDataStorageService storage,
   IBottomSheetService sheets,
      ILogger<GroupsPageViewModel> logger) : base(navigationService)
        {
            _storage = storage;
            _sheets = sheets;
            _logger = logger;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await SeedDefaultGroupsIfNeededAsync();
            await LoadGroupsAsync();
        }

        // ── Seed ──────────────────────────────────────────────────────────────
        public static async Task SeedDefaultGroupsAsync(IDataStorageService storage)
        {
            try
            {
                if (Preferences.Default.Get(SeedDonePrefKey, false)) return;
                var existing = await storage.GetVaultGroupsAsync();
                if (existing.Any()) { Preferences.Default.Set(SeedDonePrefKey, true); return; }
                foreach (var (name, icon, color) in _defaultGroups)
                    await storage.SaveVaultGroupAsync(new VaultGroup { Name = name, IconKey = icon, Color = color });
                Preferences.Default.Set(SeedDonePrefKey, true);
            }
            catch { /* best-effort */ }
        }

        private async Task SeedDefaultGroupsIfNeededAsync()
        {
            try
            {
                if (Preferences.Default.Get(SeedDonePrefKey, false)) return;

                var existing = await _storage.GetVaultGroupsAsync();
                if (existing.Any()) { Preferences.Default.Set(SeedDonePrefKey, true); return; }

                foreach (var (name, icon, color) in _defaultGroups)
                {
                    await _storage.SaveVaultGroupAsync(new VaultGroup
                    {
                        Name = name,
                        IconKey = icon,
                        Color = color,
                    });
                }
                Preferences.Default.Set(SeedDonePrefKey, true);
            }
            catch (Exception ex) { _logger.LogError(ex, "SeedDefaultGroups failed"); }
        }

        // ── Load ──────────────────────────────────────────────────────────────
        private async Task LoadGroupsAsync()
        {
            IsRefreshing = true;
            try
            {
                var groups = (await _storage.GetVaultGroupsAsync()).OrderBy(g => g.Name).ToList();
                var result = new List<VaultGroupViewModel>();

                foreach (var g in groups)
                {
                    var ids = await _storage.GetCredentialIdsInGroupAsync(g.Id);
                    result.Add(new VaultGroupViewModel
                    {
                        Id = g.Id,
                        Name = g.Name,
                        IconKey = g.IconKey,
                        Color = g.Color,
                        CredentialCount = ids.Count(),
                    });
                }

                Groups = new ObservableCollection<VaultGroupViewModel>(result);
                NoGroups = Groups.Count == 0;
            }
            catch (Exception ex) { _logger.LogError(ex, "LoadGroupsAsync failed"); }
            finally { IsRefreshing = false; }
        }

        // ── Create ────────────────────────────────────────────────────────
  private async Task ExecuteCreateGroupAsync()
        {
          var name = await _sheets.PromptAsync(
       "New Group", "Enter a name for your group",
          placeholder: "e.g. Gaming, Health, Finance…",
       confirmText: "Create");
    if (string.IsNullOrWhiteSpace(name)) return;

           name = name.Trim();
       if (Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
      {
    await _sheets.AlertAsync("Name taken", "A group with that name already exists.", "OK");
       return;
}

     var colour = _palette[_nextColour++ % _palette.Length];
        try
 {
                await _storage.SaveVaultGroupAsync(new VaultGroup { Name = name, Color = colour });
     await LoadGroupsAsync();
    }
  catch (Exception ex) { _logger.LogError(ex, "CreateGroup failed"); }
        }

        // ── Group options ─────────────────────────────────────────────────
        private async Task ExecuteShowGroupOptionsAsync(VaultGroupViewModel group)
   {
         if (group is null) return;
     var options = new List<PopupPagesViewModels.BottomSheetOption>
   {
  new() { Title = "Rename", Icon = new MauiIcon().Icon(MaterialIcons.Edit),  Action = async () => await ExecuteRenameGroupAsync(group) },
    new() { Title = "Delete", Icon = new MauiIcon().Icon(MaterialIcons.Delete), Action = async () => await ExecuteDeleteGroupAsync(group) },
  };
   await _sheets.ShowAsync<Views.PopupPages.BottomSheet, PopupPagesViewModels.BottomSheetViewModel, object>(options, group.Name);
        }

   // ── Open group ────────────────────────────────────────────────────
        private async Task ExecuteOpenGroupAsync(VaultGroupViewModel group)
  {
         if (group is null) return;
  var navParams = new NavigationParameters { { "groupId", group.Id }, { "groupName", group.Name } };
       await NavigationService.NavigateAsync(nameof(Views.GroupMembersPage), navParams);
        }

       // ── Delete ────────────────────────────────────────────────────────
        private async Task ExecuteDeleteGroupAsync(VaultGroupViewModel group)
        {
            if (group is null) return;
       var confirmed = await _sheets.DestructiveConfirmAsync(
       "Delete Group", $"Delete \"{group.Name}\"? Passwords will not be deleted.",
        destructiveText: "Delete Group");
  if (!confirmed) return;
    try
    {
     await _storage.DeleteVaultGroupAsync(group.Id);
      await LoadGroupsAsync();
    }
    catch (Exception ex) { _logger.LogError(ex, "DeleteGroup failed"); }
    }

        // ── Rename ────────────────────────────────────────────────────────
        private async Task ExecuteRenameGroupAsync(VaultGroupViewModel group)
        {
if (group is null) return;
   var newName = await _sheets.PromptAsync(
       "Rename Group", "Enter a new name for this group",
      placeholder: group.Name, defaultText: group.Name, confirmText: "Rename");
  if (string.IsNullOrWhiteSpace(newName)) return;
    try
  {
      await _storage.SaveVaultGroupAsync(new VaultGroup
  {
    Id = group.Id, Name = newName.Trim(), IconKey = group.IconKey, Color = group.Color,
 });
      await LoadGroupsAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "RenameGroup failed"); }
    }

  // ── Commands ──────────────────────────────────────────────────────
        private AsyncCommand _createGroupCommand;
        public ICommand CreateGroupCommand =>
     _createGroupCommand ??= new AsyncCommand(ExecuteCreateGroupAsync);

        private AsyncCommand<VaultGroupViewModel> _openGroupCommand;
        public ICommand OpenGroupCommand =>
          _openGroupCommand ??= new AsyncCommand<VaultGroupViewModel>(ExecuteOpenGroupAsync);

        private AsyncCommand<VaultGroupViewModel> _showGroupOptionsCommand;
   public ICommand ShowGroupOptionsCommand =>
         _showGroupOptionsCommand ??= new AsyncCommand<VaultGroupViewModel>(ExecuteShowGroupOptionsAsync);

    private AsyncCommand _refreshCommand;
    public ICommand RefreshCommand =>
   _refreshCommand ??= new AsyncCommand(LoadGroupsAsync);
    }

    // ── Per-group display model ────────────────────────────────────────────
    public class VaultGroupViewModel : Prism.Mvvm.BindableBase
  {
    public Guid Id { get; set; }
    public string IconKey { get; set; } = "Folder";
        public string Color { get; set; } = "#3B82F6";

  private string _name;
 public string Name { get => _name; set => SetProperty(ref _name, value); }

      private int _credentialCount;
   public int CredentialCount
      {
            get => _credentialCount;
          set { SetProperty(ref _credentialCount, value); RaisePropertyChanged(nameof(CountText)); }
        }

        public string CountText =>
    _credentialCount == 0 ? "Empty"
        : $"{_credentialCount} item{(_credentialCount == 1 ? "" : "s")}";
    }
}

using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Mappers;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    /// <summary>
    /// Shows members of one <see cref="VaultGroup"/> and lets the user
    /// add or remove credentials.
    /// </summary>
    public class GroupMembersPageViewModel : ViewModelBase
    {
        #region Properties

        private string _groupName;
        public string GroupName { get => _groupName; set => SetProperty(ref _groupName, value); }

        private Guid _groupId;

        private ObservableCollection<CredentialView> _members = new();
        public ObservableCollection<CredentialView> Members
        {
            get => _members;
            set => SetProperty(ref _members, value);
        }

        private bool _noMembers;
        public bool NoMembers { get => _noMembers; set => SetProperty(ref _noMembers, value); }

        private bool _isRefreshing;
        public bool IsRefreshing { get => _isRefreshing; set => SetProperty(ref _isRefreshing, value); }

        #endregion

        private readonly IDataStorageService _storage;
        private readonly IBottomSheetService _sheets;
        private readonly ILogger<GroupMembersPageViewModel> _logger;
        private List<CredentialView> _allCredentials = new();

        public GroupMembersPageViewModel(
    INavigationService navigationService,
  IDataStorageService storage,
   IBottomSheetService sheets,
       ILogger<GroupMembersPageViewModel> logger) : base(navigationService)
        {
            _storage = storage;
            _sheets = sheets;
            _logger = logger;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            if (parameters.TryGetValue("groupId", out Guid gid)) _groupId = gid;
            if (parameters.TryGetValue("groupName", out string gn)) GroupName = gn;
            await LoadAsync();
        }

        // ── Load ─────────────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            IsRefreshing = true;
            try
            {
                // Both calls run on the thread pool – map on bg thread too
                var (all, memberIds) = await Task.Run(async () =>
                 {
                     var rawAll = await _storage.GetLoginItemsAsync();
                     var mapped = LoginItemMapper.Map(rawAll).ToList();
                     var ids = (await _storage.GetCredentialIdsInGroupAsync(_groupId)).ToHashSet();
                     return (mapped, ids);
                 });

                _allCredentials = all;
                var members = all.Where(c => memberIds.Contains(c.Id)).ToList();
                Members = new ObservableCollection<CredentialView>(members);
                NoMembers = Members.Count == 0;
            }
            catch (Exception ex) { _logger.LogError(ex, "GroupMembersPageViewModel.LoadAsync failed"); }
            finally { IsRefreshing = false; }
        }

        // ── Add members ───────────────────────────────────────────────────────
        private async Task ExecuteAddMembersAsync()
        {
            var currentIds = Members.Select(m => m.Id).ToHashSet();

            // Build the available list on the thread pool – can be 1000s of items
            var available = await Task.Run(() =>
          _allCredentials
            .Where(c => !currentIds.Contains(c.Id))
     .OrderBy(c => c.Domain)
          .ToList());

            if (available.Count == 0)
            {
                await _sheets.AlertAsync("Nothing to add", "All credentials are already in this group.");
                return;
            }

            // Open the dedicated searchable sheet – filtering and virtualisation
            // happen inside AddMemberSheetViewModel, keeping the UI thread free.
            var args = new AddMemberSheetArgs(
   Available: available,
              OnPicked: async picked =>
                  {
                      try
                      {
                          var ids = Members.Select(m => m.Id).Append(picked.Id).ToList();
                          await _storage.SetCredentialGroupAsync(_groupId, ids);
                          await LoadAsync();
                      }
                      catch (Exception ex) { _logger.LogError(ex, "AddMember failed"); }
                  });

            await _sheets.ShowAsync<AddMemberSheet, AddMemberSheetViewModel, object>(
                      args, "Add to group");
        }

        // ── Remove member ─────────────────────────────────────────────────────
        private async Task ExecuteRemoveMemberAsync(CredentialView item)
        {
            if (item is null) return;

            var confirmed = await _sheets.DestructiveConfirmAsync(
  "Remove from group",
       $"Remove \"{item.Domain}\" from this group?",
                destructiveText: "Remove");

            if (!confirmed) return;

            try
            {
                var ids = Members.Where(m => m.Id != item.Id).Select(m => m.Id).ToList();
                await _storage.SetCredentialGroupAsync(_groupId, ids);
                await LoadAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "RemoveMember failed"); }
        }

        // ── Commands ─────────────────────────────────────────────────────────
        private AsyncCommand _addMembersCommand;
        public ICommand AddMembersCommand =>
            _addMembersCommand ??= new AsyncCommand(ExecuteAddMembersAsync);

        private AsyncCommand<CredentialView> _removeMemberCommand;
        public ICommand RemoveMemberCommand =>
        _removeMemberCommand ??= new AsyncCommand<CredentialView>(ExecuteRemoveMemberAsync);

        private AsyncCommand _refreshCommand;
        public ICommand RefreshCommand =>
            _refreshCommand ??= new AsyncCommand(LoadAsync);
    }
}

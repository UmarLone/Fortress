using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using System.Windows.Input;

namespace Fortress.ViewModels
{
 /// <summary>
    /// ViewModel for the "Share Item" page.
    /// Sender flow: receive a vault item via nav params ? display passphrase ?
    /// user taps "Share" ? encrypt ? save temp .fortress file ? open OS share sheet.
    /// </summary>
    public class ShareItemPageViewModel : ViewModelBase
    {
      private readonly VaultShareService _shareService;
     private readonly IUserDialogs _dialogs;

        private object? _vaultItem;
        private string _itemType = string.Empty;

        // ── Bindable properties ───────────────────────────────────────────────
  private string _itemLabel = string.Empty;
        public string ItemLabel
        {
 get => _itemLabel;
            set => SetProperty(ref _itemLabel, value);
   }

        private string _itemTypeDisplay = string.Empty;
   public string ItemTypeDisplay
      {
   get => _itemTypeDisplay;
      set => SetProperty(ref _itemTypeDisplay, value);
}

        private string _passphrase = string.Empty;
        public string Passphrase
        {
            get => _passphrase;
   set => SetProperty(ref _passphrase, value);
        }

        private bool _isSharing;
        public bool IsSharing
        {
          get => _isSharing;
       set => SetProperty(ref _isSharing, value);
        }

   private string _sharingStatus = string.Empty;
        public string SharingStatus
        {
        get => _sharingStatus;
  set => SetProperty(ref _sharingStatus, value);
   }

        private bool _isPassphraseVisible = true;
        public bool IsPassphraseVisible
        {
       get => _isPassphraseVisible;
   set => SetProperty(ref _isPassphraseVisible, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private AsyncCommand? _shareCommand;
        public ICommand ShareCommand =>
          _shareCommand ??= new AsyncCommand(ExecuteShareAsync);

      private DelegateCommand? _regeneratePassphraseCommand;
    public ICommand RegeneratePassphraseCommand =>
            _regeneratePassphraseCommand ??= new DelegateCommand(
                () => Passphrase = VaultShareService.GeneratePassphrase());

      private DelegateCommand? _copyPassphraseCommand;
    public ICommand CopyPassphraseCommand =>
            _copyPassphraseCommand ??= new DelegateCommand(async () =>
            {
    await Clipboard.Default.SetTextAsync(Passphrase);
     _dialogs.ShowToast("Passphrase copied!");
      });

        private DelegateCommand? _togglePassphraseVisibilityCommand;
        public ICommand TogglePassphraseVisibilityCommand =>
          _togglePassphraseVisibilityCommand ??= new DelegateCommand(
     () => IsPassphraseVisible = !IsPassphraseVisible);

        // ── Constructor ───────────────────────────────────────────────────────
        public ShareItemPageViewModel(
 INavigationService navigationService,
   VaultShareService shareService,
            IUserDialogs dialogs) : base(navigationService)
        {
 _shareService = shareService;
      _dialogs = dialogs;
        Passphrase = VaultShareService.GeneratePassphrase();
        }

 // ── Navigation ────────────────────────────────────────────────────────
        public override void OnNavigatedTo(INavigationParameters parameters)
  {
      base.OnNavigatedTo(parameters);

  if (parameters.TryGetValue<LoginItem>("loginItem", out var login))
            {
           _vaultItem = login;
      _itemType = "login";
 ItemLabel = login.Label;
      ItemTypeDisplay = "Login";
}
 else if (parameters.TryGetValue<Authenticator>("authenticator", out var auth))
  {
     _vaultItem = auth;
         _itemType = "authenticator";
      ItemLabel = auth.Issuer;
 ItemTypeDisplay = "Authenticator";
       }
       else if (parameters.TryGetValue<CreditCardItem>("creditCard", out var card))
         {
       _vaultItem = card;
             _itemType = "creditcard";
         ItemLabel = card.Label;
                ItemTypeDisplay = "Credit Card";
     }
            else if (parameters.TryGetValue<IdentityItem>("identity", out var identity))
          {
         _vaultItem = identity;
 _itemType = "identity";
     ItemLabel = identity.Label;
ItemTypeDisplay = "Identity";
            }
     else if (parameters.TryGetValue<SecureNoteItem>("secureNote", out var note))
            {
           _vaultItem = note;
         _itemType = "securenote";
           ItemLabel = note.Label;
     ItemTypeDisplay = "Secure Note";
  }
            else if (parameters.TryGetValue<SecureItem>("secureItem", out var secureItem))
         {
        _vaultItem = secureItem;
                _itemType = "secureitem";
           ItemLabel = secureItem.Label;
   ItemTypeDisplay = secureItem.ItemType.ToString();
      }
        }

        // ── Share logic ───────────────────────────────────────────────────────
        private async Task ExecuteShareAsync()
    {
     if (_vaultItem == null || string.IsNullOrWhiteSpace(Passphrase))
 return;

   IsSharing = true;
     try
   {
            SharingStatus = "Encrypting item…";
         await Task.Delay(50); // let UI update

 // Encrypt the item (CPU-bound – run off UI thread)
      var payload = await Task.Run(() => _shareService.Encrypt(_vaultItem, _itemType, ItemLabel, Passphrase));
       var fileBytes = await Task.Run(() => _shareService.SerializeToFile(payload));

            SharingStatus = "Preparing file…";
 await Task.Delay(50);

      // Write to temp file
  var fileName = $"{SanitizeFileName(ItemLabel)}.fortress";
  var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
    await File.WriteAllBytesAsync(tempPath, fileBytes);

            SharingStatus = "Opening share sheet…";
 await Task.Delay(50);

    // Open OS share sheet
        await Share.Default.RequestAsync(new ShareFileRequest
  {
      Title = "Share FORTRESS item",
         File = new ShareFile(tempPath, "application/fortress")
       });
 }
    catch (Exception ex)
       {
    System.Diagnostics.Debug.WriteLine($"Share error: {ex.Message}");
   _dialogs.ShowToast("Failed to share item");
      }
            finally
     {
    IsSharing = false;
            SharingStatus = string.Empty;
            }
  }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "shared-item";

     var invalid = Path.GetInvalidFileNameChars();
            var clean = new char[name.Length];
            for (int i = 0; i < name.Length; i++)
       clean[i] = Array.IndexOf(invalid, name[i]) >= 0 ? '_' : name[i];

   var result = new string(clean).Trim().Replace(' ', '-').ToLowerInvariant();
       return string.IsNullOrEmpty(result) ? "shared-item" : result;
     }
    }
}

using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Views;
using System.Text.Json;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    /// <summary>
    /// ViewModel for the "Receive Item" page.
    /// Receiver flow: pick .fortress file (or receive via file association) ?
    /// enter passphrase ? decrypt ? preview ? save to vault.
    /// </summary>
    public class ReceiveItemPageViewModel : ViewModelBase
  {
        private readonly VaultShareService _shareService;
   private readonly IDataStorageService _storageService;
        private readonly IUserDialogs _dialogs;

        private SharedItemPayload? _payload;
        private string? _decryptedJson;

     // ── Bindable properties ───────────────────────────────────────────────
     private bool _isFileLoaded;
 public bool IsFileLoaded
        {
      get => _isFileLoaded;
            set { SetProperty(ref _isFileLoaded, value); RaisePropertyChanged(nameof(IsFileNotLoaded)); }
        }
        public bool IsFileNotLoaded => !_isFileLoaded;

        private string _fileName = string.Empty;
    public string FileName
        {
   get => _fileName;
       set => SetProperty(ref _fileName, value);
        }

      private string _passphrase = string.Empty;
    public string Passphrase
        {
      get => _passphrase;
          set => SetProperty(ref _passphrase, value);
        }

        private bool _isDecrypted;
     public bool IsDecrypted
        {
    get => _isDecrypted;
    set { SetProperty(ref _isDecrypted, value); RaisePropertyChanged(nameof(IsNotDecrypted)); }
   }
        public bool IsNotDecrypted => !_isDecrypted;

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

        private string _previewText = string.Empty;
        public string PreviewText
        {
 get => _previewText;
            set => SetProperty(ref _previewText, value);
        }

      private bool _isBusy;
        public bool IsBusy
 {
    get => _isBusy;
     set => SetProperty(ref _isBusy, value);
        }

      private string _busyStatus = string.Empty;
 public string BusyStatus
        {
    get => _busyStatus;
set => SetProperty(ref _busyStatus, value);
  }

        private bool _hasError;
     public bool HasError
        {
            get => _hasError;
       set => SetProperty(ref _hasError, value);
     }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
        get => _errorMessage;
         set => SetProperty(ref _errorMessage, value);
      }

      private bool _isSaved;
     public bool IsSaved
        {
   get => _isSaved;
            set => SetProperty(ref _isSaved, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private AsyncCommand? _pickFileCommand;
        public ICommand PickFileCommand =>
 _pickFileCommand ??= new AsyncCommand(ExecutePickFileAsync);

        private AsyncCommand? _decryptCommand;
     public ICommand DecryptCommand =>
     _decryptCommand ??= new AsyncCommand(ExecuteDecryptAsync);

        private AsyncCommand? _saveToVaultCommand;
        public ICommand SaveToVaultCommand =>
    _saveToVaultCommand ??= new AsyncCommand(ExecuteSaveToVaultAsync);

      private AsyncCommand? _goHomeCommand;
        public ICommand GoHomeCommand =>
  _goHomeCommand ??= new AsyncCommand(async () =>
     await NavigationService.NavigateAsync($"/{nameof(NavigationPage)}/{nameof(Views.HomePage)}"));

        // ── Constructor ───────────────────────────────────────────────────────
      public ReceiveItemPageViewModel(
            INavigationService navigationService,
 VaultShareService shareService,
     IDataStorageService storageService,
            IUserDialogs dialogs) : base(navigationService)
        {
        _shareService = shareService;
_storageService = storageService;
       _dialogs = dialogs;
        }

        // ── Navigation ────────────────────────────────────────────────────────
        public override void OnNavigatedTo(INavigationParameters parameters)
        {
base.OnNavigatedTo(parameters);

     // Support receiving a file path from an intent/file association
            if (parameters.TryGetValue<string>("filePath", out var filePath) &&
       !string.IsNullOrEmpty(filePath))
         {
     _ = LoadFileFromPathAsync(filePath);
            }
        }

// ── File picking ──────────────────────────────────────────────────────
        private async Task ExecutePickFileAsync()
        {
    try
  {
      HasError = false;
    var result = await FilePicker.Default.PickAsync(new PickOptions
                {
PickerTitle = "Select a .fortress file",
  FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
     {
       { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
            { DevicePlatform.iOS, new[] { "public.data" } },
        })
         });

       if (result == null) return;

    FileName = result.FileName;

      using var stream = await result.OpenReadAsync();
    using var ms = new MemoryStream();
       await stream.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

                LoadPayload(fileBytes);
  }
            catch (Exception ex)
            {
           SetError($"Could not read file: {ex.Message}");
            }
      }

     private async Task LoadFileFromPathAsync(string filePath)
        {
 try
       {
     HasError = false;
            if (!File.Exists(filePath))
 {
           SetError("File not found.");
              return;
           }

                FileName = Path.GetFileName(filePath);
  var fileBytes = await File.ReadAllBytesAsync(filePath);
     LoadPayload(fileBytes);
            }
            catch (Exception ex)
  {
SetError($"Could not read file: {ex.Message}");
            }
 }

   private void LoadPayload(byte[] fileBytes)
   {
        _payload = _shareService.DeserializeFromFile(fileBytes);
        if (_payload == null)
    {
            SetError("This file is not a valid FORTRESS share file.");
                return;
}

IsFileLoaded = true;
   IsDecrypted = false;
            IsSaved = false;
       ItemLabel = _payload.Label;
    ItemTypeDisplay = FormatItemType(_payload.ItemType);
        }

        // ── Decrypt ───────────────────────────────────────────────────────────
        private async Task ExecuteDecryptAsync()
        {
     if (_payload == null || string.IsNullOrWhiteSpace(Passphrase))
return;

        HasError = false;
 IsBusy = true;
     BusyStatus = "Decrypting item…";

 try
       {
      await Task.Delay(50); // let UI update

        // Run decryption off the UI thread (PBKDF2 is expensive)
_decryptedJson = await Task.Run(() =>
       _shareService.DecryptToJson(_payload, Passphrase));

    if (_decryptedJson == null)
     {
  SetError("Wrong passphrase or corrupted file. Please try again.");
     return;
          }

     BusyStatus = "Preparing preview…";
  await Task.Delay(50);

       IsDecrypted = true;
         PreviewText = BuildPreviewText(_payload.ItemType, _decryptedJson);
          }
        catch
      {
     SetError("Decryption failed. The file may be corrupted.");
   }
 finally
    {
     IsBusy = false;
     BusyStatus = string.Empty;
  }
   }

        // ── Save to vault ─────────────────────────────────────────────────────
        private async Task ExecuteSaveToVaultAsync()
     {
   if (_payload == null || _decryptedJson == null)
   return;

  IsBusy = true;
    BusyStatus = "Saving to vault…";
    try
  {
    await Task.Delay(50); // let UI update
      var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

   switch (_payload.ItemType)
    {
       case "login":
        var login = JsonSerializer.Deserialize<LoginItem>(_decryptedJson, opts);
  if (login != null)
        {
  login.Id = Guid.NewGuid(); // new ID for receiver's vault
  login.CreatedAt = DateTime.UtcNow;
         login.UpdatedAt = DateTime.UtcNow;
     await _storageService.SaveLoginItemAsync(login);
               }
     break;

   case "authenticator":
  var auth = JsonSerializer.Deserialize<Authenticator>(_decryptedJson, opts);
             if (auth != null)
 {
     auth.Id = Guid.NewGuid();
     auth.CreatedAt = DateTime.UtcNow;
       auth.UpdatedAt = DateTime.UtcNow;
    await _storageService.AddOrUpdateAuthenticatorsAsync(new List<Authenticator> { auth });
    }
     break;

              case "creditcard":
              var card = JsonSerializer.Deserialize<CreditCardItem>(_decryptedJson, opts);
  if (card != null)
             {
  card.Id = Guid.NewGuid();
   card.CreatedAt = DateTime.UtcNow;
          card.UpdatedAt = DateTime.UtcNow;
         await _storageService.SaveCreditCardItemAsync(card);
       }
 break;

        case "identity":
     var identity = JsonSerializer.Deserialize<IdentityItem>(_decryptedJson, opts);
        if (identity != null)
        {
     identity.Id = Guid.NewGuid();
           identity.CreatedAt = DateTime.UtcNow;
          identity.UpdatedAt = DateTime.UtcNow;
      await _storageService.SaveIdentityItemAsync(identity);
         }
       break;

      case "securenote":
          var note = JsonSerializer.Deserialize<SecureNoteItem>(_decryptedJson, opts);
            if (note != null)
         {
  note.Id = Guid.NewGuid();
      note.CreatedAt = DateTime.UtcNow;
       note.UpdatedAt = DateTime.UtcNow;
   await _storageService.SaveSecureNoteItemAsync(note);
             }
               break;

        case "secureitem":
         var secureItem = JsonSerializer.Deserialize<SecureItem>(_decryptedJson, opts);
         if (secureItem != null)
           {
       secureItem.Id = Guid.NewGuid();
      secureItem.CreatedAt = DateTime.UtcNow;
                  secureItem.UpdatedAt = DateTime.UtcNow;
               await _storageService.SaveSecureItemAsync(secureItem);
    }
     break;
        }

  IsSaved = true;
   _dialogs.ShowToast("Item saved to your vault!");
            }
  catch (Exception ex)
{
       SetError($"Failed to save: {ex.Message}");
    }
finally
      {
       IsBusy = false;
 BusyStatus = string.Empty;
         }
        }

  // ── Helpers ───────────────────────────────────────────────────────────
        private void SetError(string message)
        {
       HasError = true;
   ErrorMessage = message;
        }

        private static string FormatItemType(string type) => type switch
        {
   "login" => "Login",
          "authenticator" => "Authenticator",
     "creditcard" => "Credit Card",
 "identity" => "Identity",
    "securenote" => "Secure Note",
            "secureitem" => "Secure Item",
     _ => type
      };

        private static string BuildPreviewText(string type, string json)
        {
try
{
       var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return type switch
      {
            "login" => BuildLoginPreview(json, opts),
     "authenticator" => BuildAuthenticatorPreview(json, opts),
            "creditcard" => BuildCardPreview(json, opts),
        "identity" => BuildIdentityPreview(json, opts),
        "securenote" => "Secure Note",
       "secureitem" => BuildSecureItemPreview(json, opts),
 _ => "Item ready to import"
           };
            }
            catch
            {
              return "Item ready to import";
            }
 }

     private static string BuildLoginPreview(string json, JsonSerializerOptions opts)
     {
          var item = JsonSerializer.Deserialize<LoginItem>(json, opts);
  if (item == null) return "Login";
 var lines = new List<string>();
  if (!string.IsNullOrEmpty(item.Label)) lines.Add($"Site: {item.Label}");
      if (!string.IsNullOrEmpty(item.Username)) lines.Add($"Username: {item.Username}");
            if (!string.IsNullOrEmpty(item.Url)) lines.Add($"URL: {item.Url}");
            lines.Add("Password: ••••••••");
         return string.Join("\n", lines);
        }

      private static string BuildAuthenticatorPreview(string json, JsonSerializerOptions opts)
  {
     var item = JsonSerializer.Deserialize<Authenticator>(json, opts);
        if (item == null) return "Authenticator";
     var lines = new List<string>();
 if (!string.IsNullOrEmpty(item.Issuer)) lines.Add($"Issuer: {item.Issuer}");
     if (!string.IsNullOrEmpty(item.Username)) lines.Add($"Account: {item.Username}");
   lines.Add($"Type: {item.Type}");
          lines.Add($"Digits: {item.Digits}");
       return string.Join("\n", lines);
     }

        private static string BuildCardPreview(string json, JsonSerializerOptions opts)
   {
            var item = JsonSerializer.Deserialize<CreditCardItem>(json, opts);
          if (item == null) return "Credit Card";
            var lines = new List<string>();
 if (!string.IsNullOrEmpty(item.Label)) lines.Add($"Card: {item.Label}");
         if (!string.IsNullOrEmpty(item.CardholderName)) lines.Add($"Name: {item.CardholderName}");
  if (!string.IsNullOrEmpty(item.CardNetwork)) lines.Add($"Network: {item.CardNetwork}");
        lines.Add("Number: •••• ••••");
            return string.Join("\n", lines);
        }

        private static string BuildIdentityPreview(string json, JsonSerializerOptions opts)
{
            var item = JsonSerializer.Deserialize<IdentityItem>(json, opts);
            if (item == null) return "Identity";
       var lines = new List<string>();
         var name = $"{item.FirstName} {item.LastName}".Trim();
       if (!string.IsNullOrEmpty(name)) lines.Add($"Name: {name}");
   if (!string.IsNullOrEmpty(item.Email)) lines.Add($"Email: {item.Email}");
            return string.Join("\n", lines);
      }

        private static string BuildSecureItemPreview(string json, JsonSerializerOptions opts)
        {
var item = JsonSerializer.Deserialize<SecureItem>(json, opts);
     if (item == null) return "Secure Item";
      return $"{item.ItemType}: {item.Label}";
        }
    }
}

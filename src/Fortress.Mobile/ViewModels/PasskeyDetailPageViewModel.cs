using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Microsoft.Extensions.Logging;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class PasskeyDetailPageViewModel : ViewModelBase
    {
     private readonly IDataStorageService _storage;
 private readonly ILogger<PasskeyDetailPageViewModel> _logger;
   private PasskeyItem? _item;

     private string _rpId = string.Empty;
  public string RpId { get => _rpId; set => SetProperty(ref _rpId, value); }

     private string _rpName = string.Empty;
public string RpName { get => _rpName; set => SetProperty(ref _rpName, value); }

     private string _userName = string.Empty;
  public string UserName { get => _userName; set => SetProperty(ref _userName, value); }

     private string _userDisplayName = string.Empty;
  public string UserDisplayName { get => _userDisplayName; set => SetProperty(ref _userDisplayName, value); }

  private string _credentialId = string.Empty;
  public string CredentialId { get => _credentialId; set => SetProperty(ref _credentialId, value); }

     private uint _signCount;
        public uint SignCount { get => _signCount; set => SetProperty(ref _signCount, value); }

private string _createdAt = string.Empty;
  public string CreatedAt { get => _createdAt; set => SetProperty(ref _createdAt, value); }

  private string _algorithm = string.Empty;
 public string Algorithm { get => _algorithm; set => SetProperty(ref _algorithm, value); }

     public PasskeyDetailPageViewModel(
       INavigationService navigationService,
     IDataStorageService storage,
  ILogger<PasskeyDetailPageViewModel> logger)
: base(navigationService)
     {
   _storage = storage;
 _logger = logger;
  }

  public override async void OnNavigatedTo(INavigationParameters parameters)
     {
    base.OnNavigatedTo(parameters);
  if (parameters.TryGetValue("passkeyId", out string? idStr)
       && Guid.TryParse(idStr, out var id))
    await LoadAsync(id);
  }

  private async Task LoadAsync(Guid id)
     {
        try
  {
   _item = await _storage.GetPasskeyItemAsync(id);
  if (_item == null) return;

        RpId            = _item.RpId;
    RpName      = string.IsNullOrEmpty(_item.RpName) ? _item.RpId : _item.RpName;
     UserName        = _item.UserName;
    UserDisplayName = _item.UserDisplayName;
  CredentialId    = TruncateBase64(_item.CredentialId);
  SignCount       = _item.SignCount;
    CreatedAt       = _item.CreatedAt.ToLocalTime().ToString("d MMM yyyy, HH:mm");
     Algorithm       = _item.Algorithm == -7 ? "ES256 (ECDSA P-256)" : $"COSE {_item.Algorithm}";
  }
  catch (Exception ex) { _logger.LogError(ex, "PasskeyDetail: load failed for {Id}", id); }
   }

     private AsyncCommand _deleteCommand;
        public ICommand DeleteCommand =>
     _deleteCommand ??= new AsyncCommand(async () =>
 {
   if (_item == null) return;
 await _storage.DeletePasskeyItemAsync(_item.Id);
await NavigationService.GoBackAsync();
  });

 private static string TruncateBase64(string? b64)
     {
   if (string.IsNullOrEmpty(b64)) return string.Empty;
  return b64.Length <= 16 ? b64 : $"{b64[..8]}…{b64[^8..]}";
 }
    }
}

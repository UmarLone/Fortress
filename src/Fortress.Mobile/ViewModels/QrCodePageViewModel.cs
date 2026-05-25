using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Helpers;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class QrCodePageViewModel : ViewModelBase
    {
        private Guid id;
        public Guid Id
        {
            get { return id; }
            set { SetProperty(ref id, value); }
        }
        private string qrCode;
        public string QrCode
        {
            get { return qrCode; }
            set
            {
                SetProperty(ref qrCode, value);
            }
        }
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<QrCodePageViewModel> _logger;
        private readonly IDataStorageService _storageService;
        private readonly IDeviceServices _deviceServices;
        public QrCodePageViewModel(INavigationService navigationService,
            ILogger<QrCodePageViewModel> logger,
            IDeviceServices deviceServices,
            IDataStorageService storageService,
            ICryptographyService cryptographyService)
          : base(navigationService)
        {
            _logger = logger;
            _deviceServices = deviceServices;
            _storageService = storageService;
            _cryptographyService = cryptographyService;
        }
        public async override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            Id = parameters.GetValue<Guid>("Id");
            Title = parameters.GetValue<string>("Title");
            var auth = await _storageService.GetAuthenticatorAsync(x => x.Id == Id);
            if (auth != null)
            {
                if(!string.IsNullOrEmpty(auth.Secret))
                {
                    auth.Secret =  (await _cryptographyService.Decrypt(auth.Secret)).Data;
                }
                QrCode = AuthenticatorHelper.GetAuthUri(auth);
            }

        }
        #region Methods
        
        private async Task ExecuteCopyToClipboardAsync()
        {
            try
            {
                await _deviceServices.CopyToClipboard(QrCode, "Copied to clipboard", PreferenceWrapper.Instance.ClearClipboardTimeout);
                 
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed process QR code. Reason: {ex.Message}");
            }
        }

        private async Task ShareQrCodeAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(QrCode)) return;

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = $"Share {Title} QR Code",
                    Text= QrCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to share QR code");
            }
        }
        #endregion
        #region Commands

        private AsyncCommand? _copyQRCodeToClipCommand;
        public ICommand CopyQRCodeToClipCommand =>
  _copyQRCodeToClipCommand ??= new AsyncCommand(ExecuteCopyToClipboardAsync);

     private AsyncCommand? _shareCommand;
   public ICommand ShareCommand =>
    _shareCommand ??= new AsyncCommand(ShareQrCodeAsync);

      #endregion
    }
}

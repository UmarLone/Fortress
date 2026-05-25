using Fortress.Mobile.Core.Models;
using Plugin.Fingerprint.Abstractions;

namespace Fortress.Mobile.Core.Contracts
{
    public interface IDeviceServices
    {
        void CloseApplication();
        string GetAppVersion();
        Task OpenAppStore();
        Task UnregisterPush();
        Task<string> RequestPushToken();
        void PlayDefaultNotificationSound();
        bool IsBluetoothEnabled();
        bool EnableBluetooth();
        Task<bool> VerifyStoragePermissions();
        Task<bool> VerifyCameraPermissions();
        Task<bool> VerifyNetworkPermissions();
        Task<bool> VerifyMediaPermissions();
        Task<bool>VerifyNotificationPermissions();
        bool AutofillAccessibilityServiceRunning();
        void DisableAutofillService();
        void OpenAccessibilityOverlayPermissionSettings();
        void OpenAccessibilitySettings();
        
        bool IsNotificationSupported();
        ApplicationState GetApplicationState();
        void SetApplicationState(ApplicationState applicationState);
        string DecodeQrCodeImage(string filePath);
        void OpenAutofillSettings();
        Task<bool> LaunchApp(string appName);
        Task CopyToClipboard(string value, string message, int expiresInMs = -1, bool isSensitive = true);
        bool AutofillServiceEnabled(out bool isPackageNameCorrect);
        void Toast(string text, bool longDuration = false);
        
        void SetScreenCaptureAllowed(bool isAllowed);
        Task<bool> VerifyAlarmPermissions();
        IEnumerable<string> GetInstalledAppNames();
        
        /// <summary>
        /// Opens Android Credential Provider settings (Android 14+) so the
        /// user can set FORTRESS as the default passkey provider.
        /// No-op on iOS and Android &lt; 14.
        /// </summary>
        void OpenCredentialProviderSettings();
    }
}

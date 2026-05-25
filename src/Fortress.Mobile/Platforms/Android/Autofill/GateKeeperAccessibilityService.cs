using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Views.Accessibility;
using Bit.Droid.Autofill;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;

namespace com.fortress.app;

[Android.App.Service(
    Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
    Exported = true,
    Label = "FORTRESS Autofill")]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/accessibilityservice")]
public class FortressAccessibilityService : AccessibilityService
{
    private static ILogger<FortressAccessibilityService>? Log =>
        Shiny.Hosting.Host.GetService<ILogger<FortressAccessibilityService>>();
    private static int _pendingIntentId = 0;

    private string? _lastPackage;
    private DateTime _lastTriggerTime = DateTime.MinValue;

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private const string ChannelId = "fortress_autofill_v2";

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        Log?.LogInformation("[Accessibility] Service connected");
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (e == null) return;

        try
        {
            var packageName = e.PackageName?.ToString();
            if (string.IsNullOrWhiteSpace(packageName)) return;

            // 🚫 Ignore own app
            if (packageName == PackageName) return;

            // 🚫 Ignore system / noisy apps
            if (IsIgnoredPackage(packageName)) return;

            // 🎯 Only react to focus events (CRITICAL)
            if (e.EventType != EventTypes.ViewFocused &&
                e.EventType != EventTypes.ViewClicked)
                return;

            var source = e.Source;
            if (source == null) return;

            // 🔍 Detect real password field
            if (!IsRealPasswordField(source)) return;

            // ⏱️ Cooldown per package
            var now = DateTime.UtcNow;
            if (_lastPackage == packageName &&
                (now - _lastTriggerTime) < Cooldown)
                return;

            _lastPackage = packageName;
            _lastTriggerTime = now;

            Log?.LogInformation("[Accessibility] Password field detected in {Package}", packageName);

            ShowAutofillNotification(packageName);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "[Accessibility] Error");
        }
    }

    public override void OnInterrupt() { }

    // ================================
    // 🔍 Detection Logic (PRO LEVEL)
    // ================================
    private static bool IsRealPasswordField(AccessibilityNodeInfo node)
    {
        if (node == null) return false;

        var className = node.ClassName?.ToString() ?? "";

        // ✅ Must be input field
        if (!className.Contains("EditText", StringComparison.OrdinalIgnoreCase))
            return false;

        // ✅ Must be editable
        if (!node.Editable)
            return false;

        // ✅ Must be focused (user interacting)
        if (!(node.Focused || node.AccessibilityFocused))
            return false;

        // ✅ Strong signal: password flag
        if (node.Password)
            return true;

        // ✅ Fallback: hint text check
        var hint = node.HintText?.ToString() ?? "";
        if (hint.Contains("password", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // ================================
    // 🚫 Ignore noisy/system apps
    // ================================
    private static bool IsIgnoredPackage(string packageName)
    {
        return packageName.StartsWith("com.android") ||
               packageName.StartsWith("com.samsung") ||
               packageName.Contains("systemui") ||
               packageName.Contains("launcher") ||
               packageName.Contains("keyboard");
    }

    // ================================
    // 🔔 Notification
    // ================================
    private void ShowAutofillNotification(string packageName)
    {
        try
        {
            var nm = (NotificationManager?)GetSystemService(NotificationService);
            if (nm == null) return;

            CreateNotificationChannel(nm);

            var appName = GetAppName(packageName);
            var intent = new Intent(this, typeof(AutofillActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            intent.PutExtra("autofill", true);
            intent.PutExtra("accessibilityFlow", true);
            intent.PutExtra(AutofillConstants.AutofillFramework, true);
            intent.PutExtra("autofillFrameworkUri", $"androidapp://{packageName}");
            intent.PutExtra("autofillFrameworkName", appName);
            intent.PutExtra("autofillFrameworkFillType", (int)CipherType.Login);

            bool isVaultLocked =
                    PreferenceWrapper.Instance.IsApplicationLocked &&
                    (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                           PreferenceWrapper.Instance.IsPinUnlockEnabled);
            intent.PutExtra("isVaultLocked", isVaultLocked);

            var pendingIntent = PendingIntent.GetActivity(this, ++_pendingIntentId, intent,
        PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            //var pi = PendingIntent.GetActivity(
            //    this,
            //    Java.Lang.JavaSystem.CurrentTimeMillis().GetHashCode(),
            //    intent,
            //    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var notification = new Android.App.Notification.Builder(this, ChannelId)
                .SetSmallIcon(Microsoft.Maui.Resource.Drawable.applogo)
                .SetContentTitle("Fill with Fortress")
                .SetContentText($"Tap to autofill in {appName}")
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .SetCategory(Android.App.Notification.CategoryRecommendation)
                .SetPriority((int)NotificationPriority.High)
                .Build();

            nm.Notify(Java.Lang.JavaSystem.CurrentTimeMillis().GetHashCode(), notification);

            Log?.LogInformation("[Accessibility] Notification shown for {Package}", packageName);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "[Accessibility] Notification failed");
        }
    }

    private static void CreateNotificationChannel(NotificationManager nm)
    {
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O)
            return;

        var existing = nm.GetNotificationChannel(ChannelId);
        if (existing != null) return;

        var channel = new NotificationChannel(
            ChannelId,
            "FORTRESS Autofill",
            NotificationImportance.High)
        {
            Description = "Autofill suggestions"
        };

        channel.EnableVibration(true);
        channel.EnableLights(true);

        nm.CreateNotificationChannel(channel);
    }

    // ================================
    // 📦 App Name Resolver
    // ================================
    private string GetAppName(string packageName)
    {
        try
        {
            var pm = PackageManager;
            var appInfo = pm?.GetApplicationInfo(packageName, 0);
            return pm?.GetApplicationLabel(appInfo!) ?? packageName;
        }
        catch
        {
            return packageName;
        }
    }
}
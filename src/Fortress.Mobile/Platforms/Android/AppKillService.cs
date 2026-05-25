using Android.App;
using Android.Content;
using Android.OS;
using Fortress.Mobile.Core.Services;

namespace Fortress.Mobile
{
    [Android.App.Service]
    public class AppKillService : Service
    {
        public override IBinder? OnBind(Intent intent)
        {
            return null;
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            base.OnTaskRemoved(rootIntent);
            if (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                                  PreferenceWrapper.Instance.IsPinUnlockEnabled)
            {
                PreferenceWrapper.Instance.IsApplicationLocked = true;
            }
        }
    }
}

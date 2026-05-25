using Android.Content;

namespace Fortress.Droid.Services
{
    [BroadcastReceiver(Name = "com.fortress.ClearClipboardAlarmReceiver", Exported = false)]
    public class ClearClipboardAlarmReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var clipboardManager = context.GetSystemService(Context.ClipboardService) as ClipboardManager;
            clipboardManager.PrimaryClip = ClipData.NewPlainText("fortress", " ");
        }
    }
}
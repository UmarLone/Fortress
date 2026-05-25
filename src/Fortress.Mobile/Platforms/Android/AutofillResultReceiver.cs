//using Android.App;
//using Android.Content;
//using Android.Content.PM;
//using Android.OS;
//using Android.Runtime;
//using Android.Service.Autofill;
//using Android.Views;
//using Android.Views.Autofill;
//using Fortress.Mobile;
//using Fortress.Mobile.Adapters;
//using Fortress.Mobile.Core.Services;
//using System.Data;
//using System.Diagnostics;

//namespace com.fortress.passwordmanager
//{
//    [BroadcastReceiver(Enabled = true, Exported = false)]
//    [IntentFilter(new[] { "AUTOFILL_RESULT" })]
//    [Register("com.fortress.passwordmanager.AutofillResultReceiver")]
//    public class AutofillResultReceiver : BroadcastReceiver
//    {
//        private AutofillActivity _activity;
//        public AutofillResultReceiver()
//        {
//
//        }
//        public AutofillResultReceiver(AutofillActivity parent)
//        {
//            _activity = parent;
//        }

//        public override void OnReceive(Context context, Intent intent)
//        {
//            var dataset = intent.GetParcelableExtra("dataset") as Dataset;
//            if (dataset == null)
//                return;
//            var reply = new Intent();
//            reply.PutExtra(AutofillManager.ExtraAuthenticationResult, dataset);

//            _activity.SetResult(Result.Ok, reply);
//            _activity.FinishAndRemoveTask();
//        }
//    }
//}

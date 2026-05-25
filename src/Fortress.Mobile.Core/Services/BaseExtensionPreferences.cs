
//using Foundation;

//namespace Fortress.Mobile.Core.Services
//{
//    public abstract class BaseExtensionPreferences
//    {
//        protected const string AppGroup =
//            "group.com.fortress";

//        protected NSUserDefaults Defaults =>
//            NSUserDefaults.FromSuiteName(AppGroup);

//        protected bool GetBool(string key, bool defaultValue = false)
//        {
//            if (Defaults.ObjectForKey(key) == null)
//                return defaultValue;

//            return Defaults.BoolForKey(key);
//        }

//        protected void SetBool(string key, bool value)
//        {
//            Defaults.SetBool(value, key);
//            Defaults.Synchronize();
//        }

//        protected string GetString(string key, string defaultValue = null)
//        {
//            return Defaults.StringForKey(key) ?? defaultValue;
//        }

//        protected void SetString(string key, string value)
//        {
//            if (value == null)
//                Defaults.RemoveObject(key);
//            else
//                Defaults.SetString(value, key);

//            Defaults.Synchronize();
//        }

//        protected int? GetNullableInt(string key)
//        {
//            var value = Defaults.StringForKey(key);
//            return value == null ? null : int.Parse(value);
//        }

//        protected void SetNullableInt(string key, int? value)
//        {
//            if (value == null)
//                Defaults.RemoveObject(key);
//            else
//                Defaults.SetString(value.Value.ToString(), key);

//            Defaults.Synchronize();
//        }
//    }
//}

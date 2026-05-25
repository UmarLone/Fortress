using System;

namespace Fortress.Mobile.Core.Services
{
    public abstract class BasePreferenceWrapper
    {
       
        protected BasePreferenceWrapper()
        {
        }
        protected T GetEnumPreference<T>(string key, T defaultValue) where T : Enum
        {
            return (T)(object)Preferences.Default.Get(key, (int)(object)defaultValue);
        }
        protected void SetEnumPreference<T>(string key, T value) where T : Enum
        {
            Preferences.Default.Set(key, (int)(object)value);
        }
        protected bool? GetNullableBooleanPreference(string key, bool? defaultValue)
        {
            var defaultStr = defaultValue switch
            {
                null => null,
                false => "false",
                true => "true"
            };

            return Preferences.Default.Get(key, defaultStr) switch
            {
                null => null,
                "false" => false,
                _ => true
            };
        }
        //protected void SetNullableBooleanPreference(string key, bool? value)
        //{
        //    AppSettings.AddOrUpdateValue(key, value switch
        //    {
        //        null => null,
        //        false => "false",
        //        true => "true"
        //    });
        //}
        protected int? GetNullableIntPreference(string key, int? defaultValue)
        {
            var defaultStr = defaultValue switch
            {
                null => null,
                _ => defaultValue.ToString()
            };
            var result = Preferences.Default.Get(key, defaultStr);
            return result switch
            {
                null => null,
                _ => Int32.Parse(result)
            };
        }
        //protected void SetNullableIntPreference(string key, int? value)
        //{
        //    AppSettings.AddOrUpdateValue(key, value switch
        //    {
        //        null => null,
        //        _ => value.ToString()
        //    });
        //}

        protected Uri GetUriPreference(string key, Uri defaultValue)
        {
            var value = Preferences.Get(key, null);

            return value == null
                ? defaultValue
                : new Uri(value);
        }

        protected void SetUriPreference(string key, Uri value)
        {
            SetPreference(key, value?.ToString());
        }
        protected void SetPreference(string key, string value)
        {
            if (value == null)
                Preferences.Default.Remove(key);
            else
                Preferences.Default.Set(key, value);
        }
        //protected void SetPreference(string key, string value)
        //{
        //    AppSettings.AddOrUpdateValue(key, value);
        //}

        protected void SetPreference(string key, bool value)
        {
            Preferences.Default.Set(key, value);
        }

        protected void SetPreference(string key, long value)
        {
            Preferences.Default.Set(key, value);
        }
        protected void SetNullableBooleanPreference(string key, bool? value)
        {
            if (value == null)
                Preferences.Default.Remove(key);
            else
                Preferences.Default.Set(key, value.Value ? "true" : "false");
        }
        protected void SetNullableIntPreference(string key, int? value)
        {
            if (value == null)
                Preferences.Default.Remove(key);
            else
                Preferences.Default.Set(key, value.Value.ToString());
        }
    }
}

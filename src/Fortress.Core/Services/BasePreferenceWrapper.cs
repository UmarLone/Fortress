using Fortress.Core.Contracts;

namespace Fortress.Core.Services
{
    /// <summary>
    /// Base class for preference wrappers. Uses <see cref="IPreferenceService"/>
    /// instead of Microsoft.Maui.Storage.Preferences so it compiles on any TFM.
/// </summary>
    public abstract class BasePreferenceWrapper
    {
  protected readonly IPreferenceService Prefs;

      protected BasePreferenceWrapper(IPreferenceService prefs)
        {
      Prefs = prefs;
        }

  protected T GetEnumPreference<T>(string key, T defaultValue) where T : Enum
    => (T)(object)Prefs.Get(key, (int)(object)defaultValue);

    protected void SetEnumPreference<T>(string key, T value) where T : Enum
   => Prefs.Set(key, (int)(object)value);

        protected bool? GetNullableBoolPreference(string key, bool? defaultValue)
        {
            var def = defaultValue switch { null => null, false => "false", true => "true" };
            return Prefs.Get(key, def) switch { null => null, "false" => false, _ => true };
        }

        protected void SetNullableBoolPreference(string key, bool? value)
        {
            if (value is null) Prefs.Remove(key);
  else Prefs.Set(key, value.Value ? "true" : "false");
        }

 protected int? GetNullableIntPreference(string key, int? defaultValue)
        {
    var def = defaultValue?.ToString();
            var result = Prefs.Get(key, def);
         return result is null ? null : int.Parse(result);
   }

   protected void SetNullableIntPreference(string key, int? value)
        {
       if (value is null) Prefs.Remove(key);
            else Prefs.Set(key, value.Value.ToString());
        }

        protected Uri? GetUriPreference(string key, Uri? defaultValue)
 {
       var value = Prefs.Get<string?>(key, null);
    return value is null ? defaultValue : new Uri(value);
     }

     protected void SetUriPreference(string key, Uri? value)
    => SetStringPreference(key, value?.ToString());

     protected void SetStringPreference(string key, string? value)
        {
            if (value is null) Prefs.Remove(key);
      else Prefs.Set(key, value);
        }
    }
}

namespace Fortress.Core.Contracts
{
    /// <summary>
    /// Platform-agnostic key/value preference store.
    /// On MAUI this is backed by Microsoft.Maui.Storage.Preferences.
    /// On Windows (WPF / Service) this is backed by the registry or an isolated JSON file.
    /// </summary>
    public interface IPreferenceService
    {
        T Get<T>(string key, T defaultValue);
   void Set<T>(string key, T value);
        void Remove(string key);
 void Clear();
    }
}

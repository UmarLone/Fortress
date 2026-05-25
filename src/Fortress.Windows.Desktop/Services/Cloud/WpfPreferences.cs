namespace Fortress.Windows.Desktop.Services.Cloud
{
    /// <summary>
    /// Flat key/value preferences backed by per-user IsolatedStorage.
    /// Drop-in equivalent of MAUI's Preferences.Default for the WPF project �
    /// allows the cloud sync services to use the exact same string-key storage
    /// pattern as their MAUI counterparts (GoogleDriveSyncService, etc.).
 /// </summary>
    internal static class WpfPreferences
    {
 private const string FileName = "fortress_preferences.json";

        private static Dictionary<string, string> _cache = Load();

        public static void Set(string key, string value)
    {
        _cache[key] = value;
      Persist();
        }

     public static string Get(string key, string defaultValue = "")
            => _cache.TryGetValue(key, out var v) ? v : defaultValue;

        public static void Remove(string key)
        {
          if (_cache.Remove(key)) Persist();
        }

 // ── Persistence ───────────────────────────────────────────────────────
        private static Dictionary<string, string> Load()
        {
        try
            {
    using var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForAssembly();
  if (!store.FileExists(FileName)) return [];
  using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
        FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, store);
        using var reader = new System.IO.StreamReader(stream);
              return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
      ?? [];
          }
        catch { return []; }
        }

        private static void Persist()
        {
            try
        {
          using var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForAssembly();
         using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            FileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, store);
                using var writer = new System.IO.StreamWriter(stream);
   writer.Write(System.Text.Json.JsonSerializer.Serialize(_cache));
  }
            catch { }
        }
    }
}

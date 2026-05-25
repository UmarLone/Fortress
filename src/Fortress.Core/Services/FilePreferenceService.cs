using Fortress.Core.Contracts;
using System.Text.Json;

namespace Fortress.Core.Services
{
    /// <summary>
    /// File-backed <see cref="IPreferenceService"/> for any non-MAUI host
    /// (Fortress.Service Windows Service, Fortress.Windows.Desktop WPF app).
    /// Stores preferences as a flat JSON dictionary at a stable local path.
    /// Thread-safe: all reads/writes are protected by a ReaderWriterLockSlim.
    /// </summary>
    public sealed class FilePreferenceService : IPreferenceService, IDisposable
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new();
        private Dictionary<string, JsonElement> _cache;

 private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        public FilePreferenceService(string? directory = null)
      {
    var dir = directory
     ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
       "Fortress", "Preferences");

    Directory.CreateDirectory(dir);
  _filePath = Path.Combine(dir, "preferences.json");
     _cache = Load();
        }

        // ── IPreferenceService ────────────────────────────────────────────────
      public T Get<T>(string key, T defaultValue)
        {
     _lock.EnterReadLock();
       try
       {
     if (!_cache.TryGetValue(key, out var element)) return defaultValue;
           try { return element.Deserialize<T>() ?? defaultValue; }
        catch { return defaultValue; }
         }
            finally { _lock.ExitReadLock(); }
        }

     public void Set<T>(string key, T value)
        {
            _lock.EnterWriteLock();
     try
      {
    _cache[key] = JsonSerializer.SerializeToElement(value, _jsonOpts);
 Persist();
            }
        finally { _lock.ExitWriteLock(); }
        }

     public void Remove(string key)
        {
_lock.EnterWriteLock();
            try { if (_cache.Remove(key)) Persist(); }
    finally { _lock.ExitWriteLock(); }
  }

        public void Clear()
      {
            _lock.EnterWriteLock();
            try { _cache.Clear(); Persist(); }
            finally { _lock.ExitWriteLock(); }
    }

        // ── Persistence ───────────────────────────────────────────────────────
 private Dictionary<string, JsonElement> Load()
{
            try
{
                if (!File.Exists(_filePath))
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

     var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
         var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
           foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.Clone();
       return result;
            }
       catch { return new Dictionary<string, JsonElement>(StringComparer.Ordinal); }
     }

        private void Persist()
        {
            // Called under write lock � safe to write directly.
  // Atomic write: write temp then rename to avoid corruption.
   var obj = new Dictionary<string, object?>(_cache.Count);
    foreach (var (k, v) in _cache)
        obj[k] = JsonSerializer.Deserialize<object>(v.GetRawText());

            var json = JsonSerializer.Serialize(obj, _jsonOpts);
            var tmp  = _filePath + ".tmp";
      File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
  }

   public void Dispose() => _lock.Dispose();
    }
}

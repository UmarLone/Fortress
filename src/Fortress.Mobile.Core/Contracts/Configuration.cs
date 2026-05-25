//using Newtonsoft.Json;

//public class Configuration
//{
//    private const string Key = "UniqueDeviceId";
//    private static readonly string FilePath =
//        Path.Combine(FileSystem.AppDataDirectory, "localstorage.json");

//    private static Dictionary<string, string> _fileStorage;

//    private static Dictionary<string, string> LoadFromFile()
//    {
//        if (_fileStorage != null)
//            return _fileStorage;

//        if (File.Exists(FilePath))
//        {
//            var json = File.ReadAllText(FilePath);
//            _fileStorage = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
//                           ?? new Dictionary<string, string>();
//        }
//        else
//        {
//            _fileStorage = new Dictionary<string, string>();
//        }

//        return _fileStorage;
//    }

//    private static void SaveToFile()
//    {
//        if (_fileStorage == null) return;
//        var json = JsonConvert.SerializeObject(_fileStorage, Formatting.Indented);
//        File.WriteAllText(FilePath, json);
//    }

//    public static async Task<string> GetAsync(string key)
//    {
//        if (DeviceInfo.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual)
//        {
//            var store = LoadFromFile();
//            store.TryGetValue(key, out var value);
//            return value;
//        }
//        return await SecureStorage.GetAsync(key);
//    }

//    public static async Task SetAsync(string key, string value)
//    {
//        if (DeviceInfo.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual)
//        {
//            var store = LoadFromFile();
//            store[key] = value;
//            SaveToFile();
//        }
//        else
//        {
//            await SecureStorage.SetAsync(key, value);
//        }
//    }

//    private static void Remove(string key)
//    {
//        if (DeviceInfo.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual)
//        {
//            var store = LoadFromFile();
//            if (store.Remove(key))
//                SaveToFile();
//        }
//        else
//        {
//            SecureStorage.Remove(key);
//        }
//    }

//    // ---- Public API ----

//    public static async Task<string> GetUniqueDeviceIdAsync()
//    {
//        var id = await GetAsync(Key);
//        if (string.IsNullOrEmpty(id))
//        {
//            id = Guid.NewGuid().ToString();
//            await SetAsync(Key, id);
//        }
//        return id;
//    }
//}

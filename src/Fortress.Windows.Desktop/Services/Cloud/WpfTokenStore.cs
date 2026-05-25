using System.IO;
using System.IO.IsolatedStorage;

namespace Fortress.Windows.Desktop.Services.Cloud
{
    /// <summary>
    /// Persists OAuth tokens to per-user isolated storage (no registry, no plaintext files).
    /// </summary>
    internal static class WpfTokenStore
    {
        private static string FileName(string provider) => $"fortress_{provider.ToLowerInvariant()}_tokens.json";

      public static void Save(string provider, TokenData data)
        {
         using var store = IsolatedStorageFile.GetUserStoreForAssembly();
     using var stream = new IsolatedStorageFileStream(
            FileName(provider), FileMode.Create, FileAccess.Write, store);
     using var writer = new StreamWriter(stream);
    writer.Write(System.Text.Json.JsonSerializer.Serialize(data));
  }

        public static TokenData? Load(string provider)
        {
            using var store = IsolatedStorageFile.GetUserStoreForAssembly();
   if (!store.FileExists(FileName(provider))) return null;
       using var stream = new IsolatedStorageFileStream(
         FileName(provider), FileMode.Open, FileAccess.Read, store);
 using var reader = new StreamReader(stream);
  return System.Text.Json.JsonSerializer.Deserialize<TokenData>(reader.ReadToEnd());
   }

        public static void Clear(string provider)
        {
       using var store = IsolatedStorageFile.GetUserStoreForAssembly();
            if (store.FileExists(FileName(provider)))
          store.DeleteFile(FileName(provider));
        }

     public record TokenData(
string AccessToken,
            string RefreshToken,
            DateTime TokenExpiry,
   string UserEmail,
         string UserName,
            string LastSyncTime);
  }
}

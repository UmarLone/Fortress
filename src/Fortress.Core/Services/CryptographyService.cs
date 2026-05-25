using Fortress.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.Core.Services
{
    /// <summary>
    /// AES-CBC encryption service. Uses <see cref="FortressPreferenceWrapper"/>
    /// for the master password � no MAUI dependency.
    /// </summary>
    public class CryptographyService : ICryptographyService
    {
     private const byte AesIvSize = 16;
    private const byte GcmTagSize = 16;
    private const int Iterations = 3;
  public const int KeySize = 256;
        private const string Hash = "SHA1";
      private const string Salt = "@#@2323232gdggdhgahgs@!";
   private readonly CipherMode _cipherMode = CipherMode.CBC;
        private readonly ILogger<CryptographyService>? _logger;
     private readonly FortressPreferenceWrapper _prefs;

        private byte[]? _cachedKeyBytes;
        private string? _cachedPassword;
       private readonly object _keyLock = new();

  public CryptographyService(FortressPreferenceWrapper prefs, ILogger<CryptographyService>? logger = null)
       {
    _prefs = prefs;
 _logger = logger;
  }

        private byte[] GetKeyBytes()
        {
  // Use the dedicated crypto key (actual password), not the verifier hash
            var password = string.IsNullOrEmpty(_prefs.MasterPasswordForCrypto)
         ? _prefs.DatabasePassword   // legacy fallback for existing installs
   : _prefs.MasterPasswordForCrypto;

    lock (_keyLock)
            {
       if (_cachedKeyBytes != null && string.Equals(_cachedPassword, password, StringComparison.Ordinal))
    return _cachedKeyBytes;
    var saltBytes = Encoding.UTF8.GetBytes(Salt);
   var passwordBytes = new PasswordDeriveBytes(password, saltBytes, Hash, Iterations);
_cachedKeyBytes = passwordBytes.GetBytes(KeySize / 8);
  _cachedPassword = password;
                return _cachedKeyBytes;
         }
        }

       public void InvalidateKeyCache()
        {
            lock (_keyLock) { _cachedKeyBytes = null; _cachedPassword = null; }
        }

     public async Task<CommandResult<string>> Encrypt(string plainText)
    {
       var result = new CommandResult<string>();
          if (string.IsNullOrEmpty(plainText)) { result.Data = plainText; result.ErrorMessage = "string is null or empty"; return result; }
  try
    {
         byte[] iv = RandomNumberGenerator.GetBytes(AesIvSize);
          byte[] valueBytes = Encoding.UTF8.GetBytes(plainText);
         byte[] keyBytes = GetKeyBytes();
           using var cipher = Aes.Create();
      cipher.Mode = CipherMode.CBC;
       using var encryptor = cipher.CreateEncryptor(keyBytes, iv);
    using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
  cs.Write(valueBytes, 0, valueBytes.Length);
       cs.FlushFinalBlock();
     result.Data = PackCipherData(ms.ToArray(), iv);
    cipher.Clear();
     result.Succeeded = true;
       }
            catch (Exception ex) { result.ErrorMessage = ex.Message; _logger?.LogError(ex, ex.Message); }
       return result;
        }

        public async Task<CommandResult<string>> Decrypt(string cipherText)
   {
    var result = new CommandResult<string>();
  if (string.IsNullOrEmpty(cipherText)) { result.Data = cipherText; result.ErrorMessage = "string is null or empty"; return result; }
     try
            {
    var (encryptedBytes, iv, _) = UnpackCipherData(cipherText);
   byte[] keyBytes = GetKeyBytes();
            using var cipher = Aes.Create();
  cipher.Mode = CipherMode.CBC;
   using var decryptor = cipher.CreateDecryptor(keyBytes, iv);
       using var ms = new MemoryStream(encryptedBytes);
               using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
          using var reader = new StreamReader(cs);
          result.Data = await reader.ReadToEndAsync();
     cipher.Clear();
    result.Succeeded = true;
            }
     catch (Exception ex) { _logger?.LogError(ex, ex.Message); }
        return result;
        }

    private string PackCipherData(byte[] encryptedBytes, byte[] iv)
        {
         var data = new byte[encryptedBytes.Length + iv.Length + 2];
    int i = 0;
    data[i++] = AesIvSize;
     data[i++] = GcmTagSize;
           Array.Copy(iv, 0, data, i, iv.Length); i += iv.Length;
      Array.Copy(encryptedBytes, 0, data, i, encryptedBytes.Length);
    return Convert.ToBase64String(data);
}

        private (byte[] encrypted, byte[] iv, byte tag) UnpackCipherData(string cipherText)
     {
     int i = 0;
    var d = Convert.FromBase64String(cipherText);
  byte ivSize = d[i++];
        byte tagSize = d[i++];
        byte[] iv = new byte[ivSize];
        Array.Copy(d, i, iv, 0, ivSize); i += ivSize;
         byte[] enc = new byte[d.Length - i];
     Array.Copy(d, i, enc, 0, enc.Length);
   return (enc, iv, tagSize);
        }
    }
}

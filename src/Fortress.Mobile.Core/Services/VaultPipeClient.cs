using Fortress.Mobile.Core.Models;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
  /// Named-pipe IPC client for MAUI mobile (Windows target only).
    /// Sends <see cref="IpcRequest"/>-shaped messages to Fortress.Service
    /// and returns the deserialized <see cref="IpcResponse"/>.
    ///
    /// Protocol: newline-delimited JSON, UTF-8, over \\.\pipe\FortressVault
    /// – identical to Fortress.NativeMessagingHost.PipeBridge.
    /// </summary>
    public sealed class VaultPipeClient
    {
        private const string PipeName = "FortressVault";
        private const int ConnectTimeoutMs = 3_000;
        private const int OperationTimeoutMs = 8_000;

        private static readonly JsonSerializerOptions _json = new()
    {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Sends a method call to Fortress.Service and returns the response.</summary>
        public async Task<IpcResponse> SendAsync(string method, object payload, string sessionToken = "", CancellationToken ct = default)
   {
            var request = new IpcRequest
  {
       Method = method,
           Payload = JsonSerializer.Serialize(payload, _json),
 SessionToken = sessionToken,
         };

     using var opCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            opCts.CancelAfter(OperationTimeoutMs);

            try
       {
  using var pipe = new NamedPipeClientStream(
            ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

      await pipe.ConnectAsync(ConnectTimeoutMs, opCts.Token).ConfigureAwait(false);

    var bytes = Encoding.UTF8.GetBytes(
   JsonSerializer.Serialize(request, _json) + "\n");

         await pipe.WriteAsync(bytes, opCts.Token).ConfigureAwait(false);
      await pipe.FlushAsync(opCts.Token).ConfigureAwait(false);

     var responseBytes = await ReadLineRawAsync(pipe, opCts.Token).ConfigureAwait(false);
           if (responseBytes.Length == 0)
           return ServiceUnavailable("Empty response from Fortress Service.");

  var line = Encoding.UTF8.GetString(responseBytes).Trim();
        return JsonSerializer.Deserialize<IpcResponse>(line, _json)
          ?? ServiceUnavailable("Null response deserialized.");
            }
   catch (OperationCanceledException)
  {
       return ServiceUnavailable("Fortress Service did not respond in time.");
            }
   catch (TimeoutException)
  {
            return ServiceUnavailable("Fortress Service is not running.");
    }
            catch (Exception ex)
 {
                return ServiceUnavailable($"Pipe error: {ex.Message}");
            }
        }

        /// <summary>Deserializes the <see cref="IpcResponse.Payload"/> into <typeparamref name="T"/>.</summary>
        public static T? DeserializePayload<T>(IpcResponse response)
        {
      if (!response.Success || string.IsNullOrEmpty(response.Payload))
 return default;
            try { return JsonSerializer.Deserialize<T>(response.Payload, _json); }
         catch { return default; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static async Task<byte[]> ReadLineRawAsync(Stream stream, CancellationToken ct)
   {
            var buf = new List<byte>(256);
            var oneByte = new byte[1];
   while (true)
      {
    int read = await stream.ReadAsync(oneByte, ct).ConfigureAwait(false);
        if (read == 0) break;
            if (oneByte[0] == 0x0A) break;
       if (oneByte[0] != 0x0D) buf.Add(oneByte[0]);
          }
    return buf.ToArray();
        }

        private static IpcResponse ServiceUnavailable(string message) => new()
      {
            Success = false,
            ErrorMessage = message,
        StatusCode = 503,
    Payload = "{}",
        };
  }

    // ── IPC contracts (mirrors Fortress.Core.Contracts.IpcContracts) ─────────
    // Duplicated here because Fortress.Mobile.Core cannot reference Fortress.Core
    // (different TFM: net10.0-android/ios vs net10.0).

    public sealed class IpcRequest
    {
 public string Method { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
     public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class IpcResponse
    {
        public bool Success { get; init; }
        public string Payload { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }
        public int StatusCode { get; init; } = 200;
    }
}

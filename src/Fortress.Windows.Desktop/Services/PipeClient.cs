using Fortress.Core.Contracts;
using Fortress.Core.Models;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Thin named-pipe client � mirrors the transport used by PipeBridge in
    /// Fortress.NativeMessagingHost so the desktop app communicates with
    /// Fortress.Service exactly the same way the browser extension does.
    ///
    /// Every public method connects, sends one request, reads one response,
    /// and disconnects.  The pipe is never held open between calls.
 ///
    /// Thread-safety: each call creates its own <see cref="NamedPipeClientStream"/>;
    /// concurrent calls are fully independent.
    /// </summary>
    public sealed class PipeClient
    {
        public const string PipeName = "FortressVault";

        /// <summary>
        /// Timeout applied to the entire connect+write+read cycle per call.
        /// The service should respond within milliseconds; 8 s matches PipeBridge.
        /// </summary>
        private const int OperationTimeoutMs = 8_000;

   private static readonly JsonSerializerOptions _json = new()
     {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
      };

        // ── Core send/receive ─────────────────────────────────────────────────
        /// <summary>
        /// Sends <paramref name="request"/> to the service and returns the
        /// deserialized <see cref="IpcResponse"/>.
 /// </summary>
        public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken ct = default)
     {
    using var opCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            opCts.CancelAfter(OperationTimeoutMs);

            try
         {
             using var pipe = new NamedPipeClientStream(
         ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

         await pipe.ConnectAsync(3_000, opCts.Token).ConfigureAwait(false);

     // Write request as UTF-8 JSON + newline (matches PipeServer.ReadLineRawAsync)
              var requestBytes = Encoding.UTF8.GetBytes(
      JsonSerializer.Serialize(request, _json) + "\n");

                await pipe.WriteAsync(requestBytes, opCts.Token).ConfigureAwait(false);
                await pipe.FlushAsync(opCts.Token).ConfigureAwait(false);

    // Read response line
           var responseBytes = await ReadLineRawAsync(pipe, opCts.Token).ConfigureAwait(false);

             if (responseBytes.Length == 0)
      return ServiceUnavailable("Empty response from Fortress Service.");

     var line = Encoding.UTF8.GetString(responseBytes).Trim();
            return JsonSerializer.Deserialize<IpcResponse>(line, _json)
       ?? ServiceUnavailable("Null response from Fortress Service.");
 }
          catch (OperationCanceledException)
     {
    return ServiceUnavailable("Fortress Service did not respond in time (8 s timeout).");
  }
    catch (TimeoutException)
      {
             return ServiceUnavailable(
        "Could not connect to Fortress Service. Make sure it is running.");
}
            catch (Exception ex)
            {
    return ServiceUnavailable($"Pipe error: {ex.Message}");
            }
     }

        // ── Typed helpers ─────────────────────────────────────────────────────
        /// <summary>
        /// Sends a method call and parses the inner payload as <typeparamref name="T"/>.
        /// Returns (default, errorMessage) on failure.
        /// </summary>
        public async Task<(T? Result, string? Error)> CallAsync<T>(
            string method,
  object payload,
        string sessionToken = "",
      CancellationToken ct = default)
        {
            var request = new IpcRequest
            {
    Method       = method,
        Payload      = JsonSerializer.Serialize(payload, _json),
       SessionToken = sessionToken,
            };

       var response = await SendAsync(request, ct).ConfigureAwait(false);

if (!response.Success)
     return (default, response.ErrorMessage ?? $"Service returned {response.StatusCode}");

        try
            {
      var result = JsonSerializer.Deserialize<T>(response.Payload, _json);
            return (result, null);
 }
            catch (Exception ex)
       {
          return (default, $"Failed to parse service response: {ex.Message}");
    }
        }

        /// <summary>
        /// Sends a fire-and-check call where only success/failure matters (no typed result).
        /// </summary>
        public async Task<(bool Ok, string? Error)> CallAsync(
   string method,
            object payload,
       string sessionToken = "",
       CancellationToken ct = default)
        {
    var request = new IpcRequest
            {
       Method = method,
          Payload      = JsonSerializer.Serialize(payload, _json),
                SessionToken = sessionToken,
          };

            var response = await SendAsync(request, ct).ConfigureAwait(false);
        return (response.Success, response.Success ? null : response.ErrorMessage);
        }

        // ── Transport helpers ─────────────────────────────────────────────────
        private static async Task<byte[]> ReadLineRawAsync(Stream stream, CancellationToken ct)
{
            var buf     = new List<byte>(512);
  var oneByte = new byte[1];
       while (true)
   {
   int read = await stream.ReadAsync(oneByte, ct).ConfigureAwait(false);
      if (read == 0) break;
           if (oneByte[0] == 0x0A) break;   // newline
            if (oneByte[0] != 0x0D) buf.Add(oneByte[0]); // skip CR
         }
    return buf.ToArray();
        }

   private static IpcResponse ServiceUnavailable(string message) => new()
{
   Success = false,
            ErrorMessage = message,
StatusCode   = 503,
  Payload    = "{}",
        };
    }
}

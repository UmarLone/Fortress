using Fortress.Core.Contracts;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fortress.NativeMessagingHost
{
    internal sealed class PipeBridge
    {
        private const string PipeName = "FortressVault";

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly Stream _stdin  = Console.OpenStandardInput();
        private static readonly Stream _stdout = Console.OpenStandardOutput();

        // Append-only log file — stdout is reserved for the native messaging protocol.
        private static readonly string _logPath =
            Path.Combine(Path.GetTempPath(), "fortress-bridge.log");

        private static void Log(string msg)
        {
            try { File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff}  {msg}\n"); }
            catch { }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Log("=== PipeBridge started ===");
            while (!ct.IsCancellationRequested)
            {
                IpcRequest? request;
                try
                {
                    request = await ReadMessageAsync<IpcRequest>(ct);
                }
                catch (EndOfStreamException)       { Log("stdin closed — exiting"); break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)               { Log($"ReadMessage error: {ex.Message}"); break; }

                if (request is null) { Log("null request — skipping"); continue; }

                Log($"► {request.Method}  payload={request.Payload}");
                int reqId = ExtractReqId(request.Payload);

                var response = await ForwardToPipeAsync(request, ct);

                Log($"◄ {request.Method}  success={response.Success}  status={response.StatusCode}  err={response.ErrorMessage}");
                await WriteMessageAsync(response, reqId, ct);
                Log($"✓ response written  _reqId={reqId}");
            }
            Log("=== PipeBridge exited ===");
        }

        private static async Task<IpcResponse> ForwardToPipeAsync(IpcRequest request, CancellationToken ct)
        {
            // Single CTS governing the entire pipe operation (connect + write + read).
            using var opCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            opCts.CancelAfter(8_000);

            try
            {
                Log("  pipe: connecting…");
                using var pipe = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                await pipe.ConnectAsync(3_000, opCts.Token);
                Log("  pipe: connected");

                // Raw byte write — avoids StreamWriter BOM/buffering issues.
                var requestBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(request, _json) + "\n");

                Log($"  pipe: writing {requestBytes.Length} bytes…");
                await pipe.WriteAsync(requestBytes, opCts.Token);
                await pipe.FlushAsync(opCts.Token);
                Log("  pipe: request written — waiting for response…");

                // Read raw bytes until we hit a newline.
                var responseBytes = await ReadLineRawAsync(pipe, opCts.Token);
                Log($"  pipe: got {responseBytes.Length} response bytes");

                if (responseBytes.Length == 0)
                    return ServiceUnavailable("Empty response from Fortress Service.");

                var line = Encoding.UTF8.GetString(responseBytes).Trim();
                return JsonSerializer.Deserialize<IpcResponse>(line, _json)
                    ?? ServiceUnavailable("Null response from Fortress Service.");
            }
            catch (OperationCanceledException)
            {
                Log("  pipe: operation timed out after 8 s");
                return ServiceUnavailable("Fortress Service did not respond in time.");
            }
            catch (TimeoutException)
            {
                Log("  pipe: connect timed out — service not running?");
                return ServiceUnavailable(
                    "Fortress Service is not running. Please start it from the Fortress desktop app.");
            }
            catch (Exception ex)
            {
                Log($"  pipe: exception — {ex.GetType().Name}: {ex.Message}");
                return ServiceUnavailable($"Pipe error: {ex.Message}");
            }
        }

        /// <summary>Reads bytes from a pipe until a newline byte (0x0A) is encountered.</summary>
        private static async Task<byte[]> ReadLineRawAsync(Stream stream, CancellationToken ct)
        {
            var buf = new List<byte>(256);
            var oneByte = new byte[1];
            while (true)
            {
                int read = await stream.ReadAsync(oneByte, ct);
                if (read == 0) break;       // pipe closed
                if (oneByte[0] == 0x0A) break; // newline
                if (oneByte[0] != 0x0D) buf.Add(oneByte[0]); // skip CR
            }
            return buf.ToArray();
        }

        private static async Task<T?> ReadMessageAsync<T>(CancellationToken ct)
        {
            var lenBuf = new byte[4];
            await ReadExactAsync(_stdin, lenBuf, ct);
            int length = BitConverter.ToInt32(lenBuf, 0);

            if (length <= 0 || length > 1_048_576)
                throw new InvalidDataException($"Invalid message length: {length}");

            var buf = new byte[length];
            await ReadExactAsync(_stdin, buf, ct);
            return JsonSerializer.Deserialize<T>(buf, _json);
        }

        private static async Task WriteMessageAsync(IpcResponse message, int reqId, CancellationToken ct)
        {
            var node   = JsonSerializer.SerializeToNode(message, _json)!.AsObject();
            node["_reqId"] = reqId;
            var json   = Encoding.UTF8.GetBytes(node.ToJsonString());
            var lenBuf = BitConverter.GetBytes(json.Length);

            await _stdout.WriteAsync(lenBuf, ct);
            await _stdout.WriteAsync(json, ct);
            await _stdout.FlushAsync(ct);
        }

        private static int ExtractReqId(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("_reqId", out var elem))
                    return elem.GetInt32();
            }
            catch { }
            return 0;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buf, CancellationToken ct)
        {
            int offset = 0;
            while (offset < buf.Length)
            {
                int read = await stream.ReadAsync(buf.AsMemory(offset), ct);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }

        private static IpcResponse ServiceUnavailable(string message) => new()
        {
            Success      = false,
            ErrorMessage = message,
            StatusCode   = 503,
            Payload      = "{}",
        };
    }
}

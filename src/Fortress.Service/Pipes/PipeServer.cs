using Fortress.Core.Contracts;
using Fortress.Service.Infrastructure;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fortress.Service.Pipes;

/// <summary>
/// Named-pipe IPC server at \\.\pipe\FortressVault.
//
// Protocol  : newline-delimited JSON  IpcRequest → IpcResponse  (UTF-8)
/// Concurrency: a fresh NamedPipeServerStream is created for every accepted
/// connection; each connection is dispatched on its own Task.
///
/// Caller detection
/// ─────────────────
///   Extension  – autofill methods: GetCredentials, FillConfirmed, SaveNewLogin
///   Desktop    – CRUD methods:     GetLoginItems, SaveLoginItem, … and session methods
///   Unknown  – anything else
/// </summary>
public sealed class PipeServer : IHostedService, IDisposable
{
    public const string PipeName = "FortressVault";

    private readonly IServiceProvider _services;
    private readonly ILogger<PipeServer> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private int _totalConnections;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // ── Caller classification ─────────────────────────────────────────────────
    private static readonly HashSet<string> ExtensionMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetCredentials", "FillConfirmed", "SaveNewLogin",
    "UnlockWithPassword", "UnlockWithPin", "Lock", "GetStatus",
    "CompleteSetup",
    };

    private static readonly HashSet<string> DesktopMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetLoginItems",    "SaveLoginItem",    "DeleteLoginItem",    "UpdateLogin",
   "GetCreditCards",   "SaveCreditCard",   "DeleteCreditCard", "UpdateCard",
        "GetAuthenticators","SaveAuthenticator","DeleteAuthenticator","UpdateAuthenticator",
  "GetSecureItems",   "SaveSecureItem",   "DeleteSecureItem",   "UpdateSecureItem",
        "GetIdentities",    "SaveIdentity",     "DeleteIdentity","UpdateIdentity",
        "GetSecureNotes",   "SaveSecureNote",   "UpdateSecureNote",   "DeleteSecureNote",
        "DeleteItem",       "ToggleFavorite",
    };

    /// <summary>JSON property names that must never appear verbatim in log output.</summary>
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "credential", "pin", "cvv", "number", "otpSecret",
        "sessionToken", "token", "privateKey", "sshPassword", "noteContent",
    "content", "secret", "masterPassword",
    };

    public PipeServer(IServiceProvider services, ILogger<PipeServer> logger)
    {
        _services = services;
        _logger = logger;
    }

    // ── IHostedService ────────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("[PipeServer] Listening on {Pipe}", $@"\\.\pipe\{PipeName}");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        try { if (_listenTask is not null) await _listenTask; }
        catch (OperationCanceledException) { }
        _logger.LogInformation("[PipeServer] Stopped. Total connections: {Total}", _totalConnections);
    }

    // ── Listen loop ───────────────────────────────────────────────────────────

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(
                  PipeName,
                PipeDirection.InOut,
     NamedPipeServerStream.MaxAllowedServerInstances,
              PipeTransmissionMode.Byte,
       PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                var connId = Interlocked.Increment(ref _totalConnections);
                _logger.LogDebug("[PipeServer] ↑ Client connected (conn #{Id})", connId);

                _ = Task.Run(() => HandleConnectionAsync(pipe, connId, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PipeServer] Error in listen loop — retrying in 500 ms.");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    // ── Per-connection handler ────────────────────────────────────────────────

    private async Task HandleConnectionAsync(
        NamedPipeServerStream pipe, int connId, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using (pipe)
        {
            try
            {
                var lineBytes = await ReadLineRawAsync(pipe, ct);
                var line = lineBytes.Length > 0
     ? System.Text.Encoding.UTF8.GetString(lineBytes).Trim()
           : null;

                if (string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogDebug("[PipeServer] conn #{Id} — empty request, closing.", connId);
                    return;
                }

                IpcRequest? req;
                try { req = JsonSerializer.Deserialize<IpcRequest>(line, _json); }
                catch
                {
                    _logger.LogWarning("[PipeServer] conn #{Id} — invalid JSON payload.", connId);
                    await WriteResponseAsync(pipe, Error(400, "Invalid JSON."));
                    return;
                }

                if (req is null)
                {
                    await WriteResponseAsync(pipe, Error(400, "Empty request."));
                    return;
                }

                // ── Structured request log ────────────────────────────────
                var caller = ClassifyCaller(req.Method);
                var preview = SanitizePayload(req.Payload);
                var hasToken = !string.IsNullOrEmpty(req.SessionToken);

                _logger.LogInformation(
           "[IPC ►] {Caller,-10} {Method,-28} token={HasToken}  payload={Payload}",
             caller, req.Method, hasToken ? "yes" : "no", preview);

                var response = await DispatchAsync(req, ct);
                await WriteResponseAsync(pipe, response);

                sw.Stop();

                // ── Structured response log ───────────────────────────────
                var statusEmoji = response.StatusCode switch
                {
                    200 => "✓",
                    401 => "✗ 401",
                    404 => "✗ 404",
                    500 => "✗ 500",
                    _ => $"? {response.StatusCode}",
                };

                if (response.Success)
                {
                    _logger.LogInformation(
                       "[IPC ◄] {Caller,-10} {Method,-28} {Status}  ({Ms} ms)",
                  caller, req.Method, statusEmoji, sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning(
          "[IPC ◄] {Caller,-10} {Method,-28} {Status}  ({Ms} ms)  err={Error}",
                           caller, req.Method, statusEmoji, sw.ElapsedMilliseconds,
                    response.ErrorMessage ?? "—");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[PipeServer] conn #{Id} — unhandled error.", connId);
            }
            finally
            {
                sw.Stop();
                _logger.LogDebug("[PipeServer] ↓ Disconnected (conn #{Id})  total={Ms} ms",
              connId, sw.ElapsedMilliseconds);
            }
        }
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private async Task<IpcResponse> DispatchAsync(IpcRequest req, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;
        var session = sp.GetRequiredService<VaultSessionService>();
        var autofill = sp.GetRequiredService<AutofillHandler>();
        var vaultItems = sp.GetRequiredService<VaultItemHandler>();

        try
        {
            return req.Method switch
            {
                // ── Session ───────────────────────────────────────────────
                "UnlockWithPassword" => HandleUnlockWithPassword(session, req),
                "UnlockWithPin" => HandleUnlockWithPin(session, req),
                "Lock" => HandleLock(session),
                "GetStatus" => HandleGetStatus(session),
                "CompleteSetup" => HandleCompleteSetup(session, req),

                // ── Autofill ──────────────────────────────────────────────
                "GetCredentials" => await HandleGetCredentials(autofill, req),
                "FillConfirmed" => await HandleFillConfirmed(autofill, req),
                "SaveNewLogin" => await HandleSaveNewLogin(autofill, req),

                // ── Login items ───────────────────────────────────────────
                "GetLoginItems" => await Handle<GetLoginItemsRequest>(req, vaultItems.GetLoginItemsAsync),
                "SaveLoginItem" => await Handle<SaveLoginItemRequest>(req, vaultItems.SaveLoginItemAsync),
                "DeleteLoginItem" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteLoginItemAsync),

                // ── Authenticators ────────────────────────────────────────
                "GetAuthenticators" => await Handle<GetAuthenticatorsRequest>(req, vaultItems.GetAuthenticatorsAsync),
                "SaveAuthenticator" => await Handle<SaveAuthenticatorRequest>(req, vaultItems.SaveAuthenticatorAsync),
                "DeleteAuthenticator" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteAuthenticatorAsync),

                // ── Credit cards ──────────────────────────────────────────
                "GetCreditCards" => await Handle<GetCreditCardsRequest>(req, vaultItems.GetCreditCardsAsync),
                "SaveCreditCard" => await Handle<SaveCreditCardRequest>(req, vaultItems.SaveCreditCardAsync),
                "DeleteCreditCard" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteCreditCardAsync),

                // ── Secure items ──────────────────────────────────────────
                "GetSecureItems" => await Handle<GetSecureItemsRequest>(req, vaultItems.GetSecureItemsAsync),
                "SaveSecureItem" => await Handle<SaveSecureItemRequest>(req, vaultItems.SaveSecureItemAsync),
                "DeleteSecureItem" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteSecureItemAsync),

                // ── Identities ────────────────────────────────────────────
                "GetIdentities" => await Handle<GetIdentitiesRequest>(req, vaultItems.GetIdentitiesAsync),
                "SaveIdentity" => await Handle<SaveIdentityRequest>(req, vaultItems.SaveIdentityAsync),
                "DeleteIdentity" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteIdentityAsync),
                "UpdateIdentity" => await Handle<UpdateIdentityRequest>(req, vaultItems.UpdateIdentityAsync),

                // ── Secure notes ──────────────────────────────────────────
                "GetSecureNotes" => await Handle<GetSecureNotesRequest>(req, vaultItems.GetSecureNotesAsync),
                "SaveSecureNote" => await Handle<SaveSecureNoteRequest>(req, vaultItems.SaveSecureNoteAsync),
                "UpdateSecureNote" => await Handle<UpdateSecureNoteRequest>(req, vaultItems.UpdateSecureNoteAsync),
                "DeleteSecureNote" => await Handle<DeleteItemRequest>(req, vaultItems.DeleteSecureNoteAsync),

                // ── Update ────────────────────────────────────────────────
                "UpdateLogin" => await Handle<UpdateLoginRequest>(req, vaultItems.UpdateLoginAsync),
                "UpdateCard" => await Handle<UpdateCreditCardRequest>(req, vaultItems.UpdateCreditCardAsync),
                "UpdateAuthenticator" => await Handle<UpdateAuthenticatorRequest>(req, vaultItems.UpdateAuthenticatorAsync),
                "UpdateSecureItem" => await Handle<UpdateSecureItemRequest>(req, vaultItems.UpdateSecureItemAsync),

                // ── Generic ───────────────────────────────────────────────
                "DeleteItem" => await Handle<GenericDeleteRequest>(req, vaultItems.DeleteItemAsync),
                "ToggleFavorite" => await Handle<ToggleFavoriteRequest>(req, vaultItems.ToggleFavoriteAsync),

                _ => Error(404, $"Unknown method: '{req.Method}'"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PipeServer] Unhandled exception in {Method}.", req.Method);
            return Error(500, ex.Message);
        }
    }

    // ── Generic CRUD helper ───────────────────────────────────────────────────

    private async Task<IpcResponse> Handle<TReq>(
        IpcRequest req,
        Func<TReq, Task<object>> handler)
    {
        var inner = Deserialize<TReq>(req.Payload);
        var result = await handler(inner);

        if (result is { } r)
        {
            var json = JsonSerializer.Serialize(r, _json);
            if (json.Contains("Unauthorized"))
                return Error(401, "Vault is locked or token is invalid.");
        }

        return Ok(result);
    }

    // ── Session handlers ──────────────────────────────────────────────────────

    private static IpcResponse HandleUnlockWithPassword(VaultSessionService session, IpcRequest req)
    {
        var inner = Deserialize<VaultActionRequest>(req.Payload);
        var result = session.UnlockWithPassword(inner.Credential ?? string.Empty);
        return result.Succeeded
          ? Ok(new { token = result.Data })
   : Error(401, result.ErrorMessage ?? "Unlock failed.");
    }

    private static IpcResponse HandleUnlockWithPin(VaultSessionService session, IpcRequest req)
    {
        var inner = Deserialize<VaultActionRequest>(req.Payload);
        var result = session.UnlockWithPin(inner.Credential ?? string.Empty);
        return result.Succeeded
            ? Ok(new { token = result.Data })
           : Error(401, result.ErrorMessage ?? "Unlock failed.");
    }

    private static IpcResponse HandleLock(VaultSessionService session)
    {
        session.Lock();
        return Ok(new { locked = true });
    }

    private static IpcResponse HandleGetStatus(VaultSessionService session)
        => Ok(session.GetSessionInfo());

    private static IpcResponse HandleCompleteSetup(VaultSessionService session, IpcRequest req)
    {
        var inner = Deserialize<CompleteSetupRequest>(req.Payload);
        var result = session.CompleteSetup(inner.MasterPassword, inner.Pin);
        return result.Succeeded
            ? Ok(new { token = result.Data, setupComplete = true })
            : Error(409, result.ErrorMessage ?? "Setup failed.");
    }

    // ── Autofill handlers ─────────────────────────────────────────────────────

    private async Task<IpcResponse> HandleGetCredentials(AutofillHandler handler, IpcRequest req)
    {
        var inner = Deserialize<GetCredentialsRequest>(req.Payload);
        var result = await handler.HandleGetCredentialsAsync(inner);
        return Ok(result);
    }

    private async Task<IpcResponse> HandleFillConfirmed(AutofillHandler handler, IpcRequest req)
    {
        var inner = Deserialize<FillConfirmedRequest>(req.Payload);
        var result = await handler.HandleFillConfirmedAsync(inner);
        return Ok(result);
    }

    private async Task<IpcResponse> HandleSaveNewLogin(AutofillHandler handler, IpcRequest req)
    {
        var inner = Deserialize<SaveNewLoginRequest>(req.Payload);
        await handler.SaveNewLoginAsync(inner);
        return Ok(new { saved = true });
    }

    // ── Caller classification ─────────────────────────────────────────────────

    private static string ClassifyCaller(string method)
    {
        if (ExtensionMethods.Contains(method)) return "Extension";
        if (DesktopMethods.Contains(method)) return "Desktop";
        return "Unknown";
    }

    // ── Payload sanitiser ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a compact JSON preview of the payload with all sensitive fields
    /// replaced by "***" so credentials never appear in log output.
    /// Falls back to a safe truncated string if the payload is not valid JSON.
    /// </summary>
    private static string SanitizePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload == "{}")
            return "{}";

        try
        {
            var node = JsonNode.Parse(payload);
            if (node is JsonObject obj)
            {
                RedactObject(obj);
                // Keep the preview to ≤120 chars so log lines stay readable
                var s = obj.ToJsonString();
                return s.Length > 120 ? s[..117] + "…" : s;
            }
        }
        catch { /* non-JSON payload — return safe truncation */ }

        return payload.Length > 60 ? payload[..57] + "…" : payload;
    }

    private static void RedactObject(JsonObject obj)
    {
        foreach (var key in obj.Select(kv => kv.Key).ToList())
        {
            if (SensitiveKeys.Contains(key))
            {
                obj[key] = "***";
            }
            else if (obj[key] is JsonObject nested)
            {
                RedactObject(nested);
            }
        }
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, _json)
        ?? throw new InvalidOperationException("Payload was null after deserialization.");

    private static IpcResponse Ok<T>(T payload) => new()
    {
        Success = true,
        Payload = JsonSerializer.Serialize(payload, _json),
        StatusCode = 200,
    };

    private static IpcResponse Error(int code, string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = code,
        Payload = "{}",
    };

    private static async Task WriteResponseAsync(Stream stream, IpcResponse response)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(response, _json) + "\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    /// <summary>Reads raw bytes from a stream until a newline (0x0A) or EOF.</summary>
    private static async Task<byte[]> ReadLineRawAsync(Stream stream, CancellationToken ct)
    {
        var buf = new List<byte>(256);
        var oneByte = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(oneByte, ct);
            if (read == 0) break;
            if (oneByte[0] == 0x0A) break;
            if (oneByte[0] != 0x0D) buf.Add(oneByte[0]);
        }
        return buf.ToArray();
    }

    public void Dispose() => _cts?.Dispose();
}

using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Services
{
    /// <summary>
    /// Hardened vault import engine (v2).
    ///
    /// Supported formats
    /// ─────────────────
    ///   CSV  : Chrome, Edge, Firefox, Safari, Bitwarden, 1Password, Dashlane, LastPass, Generic
    ///   JSON : Bitwarden, 1Password, Dashlane
    ///   Auth : Aegis JSON, Raivo JSON, 2FAS JSON, otpauth:// URI list,
    ///   otpauth-migration:// (Google Authenticator QR export)
    ///
    /// Security guarantees
    /// ───────────────────
    ///   • Raw credentials are never written to any log or diagnostic output.
    ///   • Password reuse is detected via per-session HMAC-SHA256 with an
    ///     ephemeral random key that is zeroed after the preview finishes.
    ///   • File byte buffer is zeroed via CryptographicOperations.ZeroMemory
    ///     and the temp file is deleted in the finally block.
    ///   • Unknown CSV columns are preserved as encrypted ImportedFields so
    ///   no source data is silently discarded.
    ///   • A non-sensitive ImportAuditRecord is persisted after each commit.
    ///
    /// Per-import pipeline
    /// ───────────────────
    ///   1. Stream file in 4 MB chunks ? detect BOM / encoding ? decode string
    ///   2. Detect format (JSON root keys ? CSV header fingerprints ? generic)
    ///   3. Parse into ImportCandidate list
    ///      a) Multi-URL support (Bitwarden/1Password style)
    ///      b) TOTP extraction (otpauth:// URIs, bare secrets, notes enrichment,
    ///         password-field-is-otpauth edge case)
    /// c) Unknown columns ? ImportedFields
    ///   4. Canonicalise all URLs
    ///   5. Score every password via VaultHealthCalculator
    ///   6. Detect reused passwords (HMAC-SHA256, ephemeral key)
    ///   7. Detect duplicates vs. live vault
    ///   8. Build ImportPreview — no credentials in returned warnings
    ///   9. Zero file buffer + delete temp file
    ///  10. (CommitAsync) Encrypt + persist + write ImportAuditRecord
    /// </summary>
    public sealed class VaultImportService : IVaultImportService
    {
        private readonly IDataStorageService _storage;
        private readonly ICryptographyService _crypto;
        private readonly VaultHealthCalculator _health;
        private readonly ILogger<VaultImportService> _logger;

        private const int MaxInlineBytes = 4 * 1024 * 1024;

        public VaultImportService(
            IDataStorageService storage,
      ICryptographyService crypto,
      VaultHealthCalculator health,
  ILogger<VaultImportService> logger)
        {
            _storage = storage;
            _crypto = crypto;
            _health = health;
            _logger = logger;
        }

        // ── Public API ────────────────────────────────────────────────────────
        public async Task<ImportPreview> PreviewAsync(
          string filePath,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("ImportService: PreviewAsync — file={F}", Path.GetFileName(filePath));

            byte[]? rawBytes = null;
            using var hmac = new ImportHmacProvider();
            try
            {
                rawBytes = await ReadFileSafeAsync(filePath, cancellationToken);
                var text = DecodeText(rawBytes);
                CryptographicOperations.ZeroMemory(rawBytes);
                rawBytes = null;

                var format = DetectFormat(text, filePath);
                var candidates = ParseCandidates(text, format);

                var extraWarnings = new List<string>();
                if (format == ImportSourceFormat.BitwardenJSON && candidates.Count == 0 &&
         text.Contains("\"encrypted\":true", StringComparison.OrdinalIgnoreCase))
                {
                    extraWarnings.Add(
                       "This file is a Bitwarden encrypted export and cannot be imported directly. " +
                            "Please export an unencrypted JSON from Bitwarden " +
                           "(Account Settings ? Export Vault ? Format: JSON) and try again.");
                }

                foreach (var c in candidates)
                    CanonicaliseUrls(c);

                foreach (var c in candidates)
                    (c.PasswordStrengthScore, c.PasswordStrengthLevel) = _health.ScorePassword(c.Password);

                MarkReusedPasswords(candidates, hmac);
                await MarkDuplicatesAsync(candidates, cancellationToken);

                var warnings = BuildWarnings(candidates, format);
                warnings.InsertRange(0, extraWarnings);

                var preview = new ImportPreview
                {
                    DetectedFormat = format,
                    TotalRows = candidates.Count,
                    ValidRows = candidates.Count(c => !string.IsNullOrWhiteSpace(c.Label) || c.Urls.Count > 0),
                    SkippedRows = candidates.Count(c => string.IsNullOrWhiteSpace(c.Label) && c.Urls.Count == 0 && string.IsNullOrWhiteSpace(c.Username)),
                    DuplicateCount = candidates.Count(c => c.IsDuplicate),
                    WeakCount = candidates.Count(c => c.IsWeak),
                    ReusedCount = candidates.Count(c => c.IsReused),
                    StrongCount = candidates.Count(c => !c.IsWeak),
                    TotpCount = candidates.Count(c => c.HasTotp),
                    Candidates = candidates.AsReadOnly(),
                    Warnings = warnings.AsReadOnly(),
                };

                _logger.LogInformation(
       "ImportService: preview done — format={F} total={T} dupes={D} weak={W} totp={P}",
               format, candidates.Count, preview.DuplicateCount, preview.WeakCount, preview.TotpCount);

                return preview;
            }
            finally
            {
                if (rawBytes != null)
                    CryptographicOperations.ZeroMemory(rawBytes);
                TryDeleteTempFile(filePath);
            }
        }

        public async Task<ImportResult> CommitAsync(
     ImportPreview preview,
       DuplicateMergeOption mergeOption,
      CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
           "ImportService: CommitAsync — {N} candidates, merge={M}",
                preview.Candidates.Count, mergeOption);

            int imported = 0, skipped = 0, replaced = 0, failed = 0;
            var errors = new List<string>();

            foreach (var candidate in preview.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(candidate.Label) &&
                      candidate.Urls.Count == 0 &&
                      string.IsNullOrWhiteSpace(candidate.Username))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (candidate.IsDuplicate)
                    {
                        switch (mergeOption)
                        {
                            case DuplicateMergeOption.KeepExisting:
                                skipped++;
                                continue;

                            case DuplicateMergeOption.ReplaceWithImported:
                                await SaveLoginItemAsync(candidate, existingId: candidate.DuplicateOfId);
                                replaced++;
                                continue;
                        }
                    }

                    await SaveLoginItemAsync(candidate, existingId: null);
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ImportService: failed on row {Row}", candidate.SourceRowIndex);
                    errors.Add($"Row {candidate.SourceRowIndex}: {ex.Message}");
                    failed++;
                }
            }

            _logger.LogInformation(
                "ImportService: commit done — imported={I} skipped={S} replaced={R} failed={F}",
     imported, skipped, replaced, failed);

            await SaveAuditAsync(preview, mergeOption, imported, skipped, replaced, failed);

            return new ImportResult
            {
                Imported = imported,
                Skipped = skipped,
                Replaced = replaced,
                Failed = failed,
                Errors = errors.AsReadOnly(),
            };
        }

        // ── File reading ──────────────────────────────────────────────────────
        private static async Task<byte[]> ReadFileSafeAsync(string path, CancellationToken ct)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException("Import file not found.", path);

            if (info.Length > MaxInlineBytes)
            {
                using var ms = new MemoryStream((int)Math.Min(info.Length, int.MaxValue));
                var buffer = ArrayPool<byte>.Shared.Rent(MaxInlineBytes);
                try
                {
                    await using var fs = File.OpenRead(path);
                    int read;
                    while ((read = await fs.ReadAsync(buffer, ct)) > 0)
                        await ms.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(buffer.AsSpan(0, MaxInlineBytes));
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                return ms.ToArray();
            }

            return await File.ReadAllBytesAsync(path, ct);
        }

        // ── Format detection ──────────────────────────────────────────────────
        private static ImportSourceFormat DetectFormat(string text, string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var head = text.Length > 4096 ? text[..4096] : text;
            var trimmedHead = head.TrimStart();

            if (ext == ".json" || trimmedHead.StartsWith('{') || trimmedHead.StartsWith('['))
            {
                if (head.Contains("\"encrypted\"") || head.Contains("\"items\""))
                    return ImportSourceFormat.BitwardenJSON;
                if (head.Contains("\"uuid\"") && head.Contains("\"details\""))
                    return ImportSourceFormat.OnePasswordJSON;
                if (head.Contains("\"credentials\"") || head.Contains("\"personalData\""))
                    return ImportSourceFormat.DashlaneJSON;
                return ImportSourceFormat.BitwardenJSON;
            }

            var firstLine = ReadFirstCsvLine(text).ToLowerInvariant();

            if (Contains(firstLine, "name") && Contains(firstLine, "url") &&
         Contains(firstLine, "username") && Contains(firstLine, "password") &&
          !Contains(firstLine, "totp") && !Contains(firstLine, "type"))
                return ImportSourceFormat.ChromeCSV;

            if (firstLine.StartsWith("url,username,password,httprealm") ||
     firstLine.StartsWith("\"url\",\"username\""))
                return ImportSourceFormat.FirefoxCSV;

            if (Contains(firstLine, "login_uri") || Contains(firstLine, "reprompt") ||
                     (Contains(firstLine, "type") && Contains(firstLine, "notes") &&
          Contains(firstLine, "name") && Contains(firstLine, "url")))
                return ImportSourceFormat.BitwardenCSV;

            if (Contains(firstLine, "title") && Contains(firstLine, "website") &&
              Contains(firstLine, "username") && Contains(firstLine, "password"))
                return ImportSourceFormat.OnePasswordCSV;

            if (Contains(firstLine, "username") && Contains(firstLine, "password") &&
 Contains(firstLine, "otpsecret"))
                return ImportSourceFormat.DashlaneCSV;

            if (Contains(firstLine, "url") && Contains(firstLine, "totp") &&
       Contains(firstLine, "grouping"))
                return ImportSourceFormat.LastPassCSV;

            if (Contains(firstLine, "title") && Contains(firstLine, "url") &&
              Contains(firstLine, "login") && !Contains(firstLine, "type"))
                return ImportSourceFormat.SafariCSV;

            return ImportSourceFormat.GenericCSV;
        }

        private static string ReadFirstCsvLine(string text)
        {
            var start = (text.Length > 0 && text[0] == '\uFEFF') ? 1 : 0;
            var nl = text.IndexOf('\n', start);
            return nl < 0 ? text[start..].Trim() : text[start..nl].TrimEnd('\r').Trim();
        }

        private static bool Contains(string haystack, string needle) =>
              haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        // ── Parsers ───────────────────────────────────────────────────────────
        private List<ImportCandidate> ParseCandidates(string text, ImportSourceFormat format)
        {
            return format switch
            {
                ImportSourceFormat.BitwardenJSON => ParseBitwardenJson(text),
                ImportSourceFormat.OnePasswordJSON => ParseOnePasswordJson(text),
                ImportSourceFormat.DashlaneJSON => ParseDashlaneJson(text),
                _ => ParseCsv(text, format),
            };
        }

        // ── CSV parser ────────────────────────────────────────────────────────
        private List<ImportCandidate> ParseCsv(string text, ImportSourceFormat format)
        {
            var rows = ParseAllCsvRows(text);
            if (rows.Count < 2) return new List<ImportCandidate>();

            var header = rows[0];
            var mapping = BuildColumnMapping(header, format);
            var knownIndices = new HashSet<int>(mapping.Values);
            var result = new List<ImportCandidate>(rows.Count - 1);

            for (int i = 1; i < rows.Count; i++)
            {
                var cols = rows[i];
                if (cols.Length == 0) continue;

                var c = new ImportCandidate { SourceRowIndex = i };
                c.Label = GetCol(cols, mapping, "label");
                c.Username = GetCol(cols, mapping, "username");
                c.Password = GetCol(cols, mapping, "password");
                c.Notes = GetCol(cols, mapping, "notes");

                var rawUrl = GetCol(cols, mapping, "url");
                if (!string.IsNullOrWhiteSpace(rawUrl))
                    c.Urls.Add(new CanonicalUrl { OriginalUrl = rawUrl });

                var totp = GetCol(cols, mapping, "totp");
                if (!string.IsNullOrWhiteSpace(totp))
                    c.TotpRaw = NormaliseTotpValue(totp);

                // Full TOTP enrichment: notes, key=value, bare Base32, password-is-otpauth
                EnrichTotpFromNotes(c);

                for (int col = 0; col < cols.Length; col++)
                {
                    if (knownIndices.Contains(col) || col >= header.Length) continue;
                    var colName = header[col].Trim('"').Trim();
                    if (string.IsNullOrWhiteSpace(colName)) continue;
                    var colVal = cols[col].Trim();
                    if (!string.IsNullOrWhiteSpace(colVal))
                        c.ImportedFields[colName] = colVal;
                }

                if (string.IsNullOrWhiteSpace(c.Label) && c.Urls.Count > 0)
                    c.Label = ExtractDomainLabel(c.Urls[0].OriginalUrl);

                result.Add(c);
            }

            return result;
        }

        private static Dictionary<string, int> BuildColumnMapping(string[] header, ImportSourceFormat format)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["label"] = new[] { "name", "title", "label", "site", "account", "service", "application", "login_name" },
                ["url"] = new[] { "url", "website", "web site", "login_uri", "uri", "domain", "hostname", "address" },
                ["username"] = new[] { "username", "user", "login", "email", "login_username", "login name", "account name" },
                ["password"] = new[] { "password", "login_password", "pass" },
                ["notes"] = new[] { "notes", "note", "comments", "extra", "comment" },
                ["totp"] = new[] { "totp", "otpsecret", "otp_secret", "authenticator key", "one-time password", "otp", "mfa_secret" },
            };

            for (int col = 0; col < header.Length; col++)
            {
                var h = header[col].Trim().Trim('"').ToLowerInvariant();
                foreach (var kvp in aliases)
                {
                    if (!map.ContainsKey(kvp.Key) &&
                  kvp.Value.Any(a => h == a || h.Contains(a, StringComparison.Ordinal)))
                    {
                        map[kvp.Key] = col;
                    }
                }
            }

            return map;
        }

        // ── Bitwarden JSON ────────────────────────────────────────────────────
        private List<ImportCandidate> ParseBitwardenJson(string text)
        {
            var result = new List<ImportCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                if (root.TryGetProperty("encrypted", out var encEl) && encEl.ValueKind == JsonValueKind.True)
                {
                    _logger.LogWarning("ImportService: Bitwarden file is an encrypted export — plaintext import not supported.");
                    return result;
                }

                var items = root.TryGetProperty("items", out var arr) ? arr : root;
                int row = 0;

                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeEl))
                    {
                        if (typeEl.GetInt32() != 1) continue;
                        if (!item.TryGetProperty("login", out var login)) continue;

                        var c = new ImportCandidate { SourceRowIndex = ++row };
                        c.Label = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        c.Notes = item.TryGetProperty("notes", out var no) ? no.GetString() ?? "" : "";
                        c.Username = login.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
                        c.Password = login.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

                        if (login.TryGetProperty("uris", out var uris))
                            foreach (var uriEl in uris.EnumerateArray())
                            {
                                var raw = uriEl.TryGetProperty("uri", out var uv) ? uv.GetString() ?? "" : "";
                                if (!string.IsNullOrWhiteSpace(raw))
                                    c.Urls.Add(new CanonicalUrl { OriginalUrl = raw });
                            }

                        if (login.TryGetProperty("totp", out var totpEl))
                        {
                            var totpVal = totpEl.GetString();
                            if (!string.IsNullOrWhiteSpace(totpVal))
                                c.TotpRaw = NormaliseTotpValue(totpVal);
                        }

                        EnrichTotpFromNotes(c);

                        if (item.TryGetProperty("fields", out var fields))
                            foreach (var field in fields.EnumerateArray())
                            {
                                var fname = field.TryGetProperty("name", out var fn) ? fn.GetString() ?? "" : "";
                                var fval = field.TryGetProperty("value", out var fv) ? fv.GetString() ?? "" : "";
                                if (!string.IsNullOrWhiteSpace(fname) && !string.IsNullOrWhiteSpace(fval))
                                    c.ImportedFields[fname] = fval;
                            }

                        if (string.IsNullOrWhiteSpace(c.Label) && c.Urls.Count > 0)
                            c.Label = ExtractDomainLabel(c.Urls[0].OriginalUrl);

                        if (!string.IsNullOrWhiteSpace(c.Label) || c.Urls.Count > 0 || !string.IsNullOrWhiteSpace(c.Username))
                            result.Add(c);

                        continue;
                    }

                    // flat / generic JSON fallback
                    {
                        var c = new ImportCandidate { SourceRowIndex = ++row };
                        c.Label = item.TryGetProperty("title", out var title) ? title.GetString() ?? ""
         : item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
                        c.Username = item.TryGetProperty("username", out var uFlat) ? uFlat.GetString() ?? "" : "";
                        c.Password = item.TryGetProperty("password", out var pFlat) ? pFlat.GetString() ?? "" : "";
                        c.Notes = item.TryGetProperty("notes", out var nFlat) ? nFlat.GetString() ?? ""
                        : item.TryGetProperty("note", out var nFlat2) ? nFlat2.GetString() ?? "" : "";

                        var rawUrl = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? ""
                      : item.TryGetProperty("website", out var webEl) ? webEl.GetString() ?? ""
                       : item.TryGetProperty("uri", out var uriEl2) ? uriEl2.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(rawUrl))
                            c.Urls.Add(new CanonicalUrl { OriginalUrl = rawUrl });

                        var totpRaw = item.TryGetProperty("totp", out var tEl) ? tEl.GetString() ?? ""
                          : item.TryGetProperty("otpSecret", out var oEl) ? oEl.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(totpRaw))
                            c.TotpRaw = NormaliseTotpValue(totpRaw);

                        if (string.IsNullOrWhiteSpace(c.Label) && c.Urls.Count > 0)
                            c.Label = ExtractDomainLabel(c.Urls[0].OriginalUrl);

                        if (!string.IsNullOrWhiteSpace(c.Label) || c.Urls.Count > 0 || !string.IsNullOrWhiteSpace(c.Username))
                            result.Add(c);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: Bitwarden JSON parse error");
            }
            return result;
        }

        // ── 1Password JSON ────────────────────────────────────────────────────
        private List<ImportCandidate> ParseOnePasswordJson(string text)
        {
            var result = new List<ImportCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                int row = 0;
                var root = doc.RootElement.ValueKind == JsonValueKind.Array
             ? doc.RootElement
              : doc.RootElement.TryGetProperty("items", out var items) ? items : doc.RootElement;

                foreach (var item in root.EnumerateArray())
                {
                    var c = new ImportCandidate { SourceRowIndex = ++row };
                    c.Label = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

                    if (item.TryGetProperty("details", out var details))
                    {
                        if (details.TryGetProperty("loginFields", out var fields))
                            foreach (var field in fields.EnumerateArray())
                            {
                                var designation = field.TryGetProperty("designation", out var d) ? d.GetString() : "";
                                var value = field.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                                if (designation == "username") c.Username = value;
                                if (designation == "password") c.Password = value;
                            }

                        c.Notes = details.TryGetProperty("notesPlain", out var notes) ? notes.GetString() ?? "" : "";

                        if (details.TryGetProperty("sections", out var sections))
                            foreach (var section in sections.EnumerateArray())
                            {
                                if (!section.TryGetProperty("fields", out var sFields)) continue;
                                foreach (var sf in sFields.EnumerateArray())
                                {
                                    var kind = sf.TryGetProperty("k", out var k) ? k.GetString() : "";
                                    if (kind == "totp" || kind == "concealed")
                                    {
                                        var val = sf.TryGetProperty("v", out var sv) ? sv.GetString() ?? "" : "";
                                        if (val.Contains("otpauth://", StringComparison.OrdinalIgnoreCase) ||
                                 (kind == "totp" && !string.IsNullOrWhiteSpace(val)))
                                            c.TotpRaw = NormaliseTotpValue(val);
                                    }
                                }
                            }

                        if (details.TryGetProperty("fields", out var extraFields))
                            foreach (var ef in extraFields.EnumerateArray())
                            {
                                var fname = ef.TryGetProperty("name", out var fn) ? fn.GetString() ?? "" : "";
                                var fval = ef.TryGetProperty("value", out var fv) ? fv.GetString() ?? "" : "";
                                if (!string.IsNullOrWhiteSpace(fname) && !string.IsNullOrWhiteSpace(fval))
                                    c.ImportedFields[fname] = fval;
                            }
                    }

                    if (item.TryGetProperty("overview", out var ov))
                    {
                        if (ov.TryGetProperty("url", out var url))
                        {
                            var raw = url.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(raw))
                                c.Urls.Add(new CanonicalUrl { OriginalUrl = raw });
                        }
                        if (ov.TryGetProperty("URLs", out var urls))
                            foreach (var u in urls.EnumerateArray())
                            {
                                var raw = u.TryGetProperty("u", out var uv) ? uv.GetString() ?? "" : u.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(raw))
                                    c.Urls.Add(new CanonicalUrl { OriginalUrl = raw });
                            }
                    }

                    result.Add(c);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: 1Password JSON parse error");
            }
            return result;
        }

        // ── Dashlane JSON ─────────────────────────────────────────────────────
        private List<ImportCandidate> ParseDashlaneJson(string text)
        {
            var result = new List<ImportCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var credArray = root.TryGetProperty("credentials", out var creds) ? creds : root;
                int row = 0;

                foreach (var item in credArray.EnumerateArray())
                {
                    var c = new ImportCandidate { SourceRowIndex = ++row };
                    c.Label = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    c.Username = item.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                    c.Password = item.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
                    c.Notes = item.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";

                    var rawUrl = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(rawUrl))
                        c.Urls.Add(new CanonicalUrl { OriginalUrl = rawUrl });

                    var otpSecret = item.TryGetProperty("otpSecret", out var otp) ? otp.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(otpSecret))
                        c.TotpRaw = NormaliseTotpValue(otpSecret);

                    if (string.IsNullOrWhiteSpace(c.Label) && c.Urls.Count > 0)
                        c.Label = ExtractDomainLabel(c.Urls[0].OriginalUrl);

                    result.Add(c);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: Dashlane JSON parse error");
            }
            return result;
        }

        // ── URL canonicalisation ──────────────────────────────────────────────
        private static void CanonicaliseUrls(ImportCandidate c)
        {
            for (int i = 0; i < c.Urls.Count; i++)
                c.Urls[i] = DomainCanonicaliser.Canonicalise(c.Urls[i].OriginalUrl);
        }

        private static string ExtractDomainLabel(string rawUrl)
        {
            var canon = DomainCanonicaliser.Canonicalise(rawUrl);
            return string.IsNullOrWhiteSpace(canon.Host) ? rawUrl : canon.Host;
        }

        // ── TOTP extraction pipeline ──────────────────────────────────────────
        /// <summary>
        /// Full TOTP detection pipeline applied to every login ImportCandidate.
        ///
        /// Detection order (first match wins):
        ///   1. Dedicated totp/otpSecret column already populated by CSV/JSON parsers
        ///   2. otpauth:// URI in Notes field
        ///   3. Key=value pair in Notes (e.g. "totp: JBSWY3DPEHPK3PXP")
        ///   4. Bare Base32 secret (? 16 chars, A-Z2-7) anywhere in Notes
        ///   5. otpauth:// URI stored as the password field (rare Bitwarden edge case)
        /// </summary>
        private static void EnrichTotpFromNotes(ImportCandidate c)
        {
            if (!string.IsNullOrWhiteSpace(c.TotpRaw))
            {
                c.TotpRaw = NormaliseTotpValue(c.TotpRaw);
                return;
            }

            var notes = c.Notes ?? string.Empty;

            // 1. otpauth:// URI embedded anywhere in notes
            var otpAuthIdx = notes.IndexOf("otpauth://", StringComparison.OrdinalIgnoreCase);
            if (otpAuthIdx >= 0)
            {
                var uriEnd = notes.IndexOfAny(new[] { ' ', '\n', '\r', '\t' }, otpAuthIdx);
                var uri = uriEnd < 0 ? notes[otpAuthIdx..] : notes[otpAuthIdx..uriEnd];
                c.TotpRaw = NormaliseTotpValue(uri.Trim());
                if (c.TotpRaw != null) return;
            }

            // 2. Key=value patterns
            var totpKeyPatterns = new[]
                    {
         "login.totp:", "totp:", "otp:", "otpsecret:", "otp_secret:",
     "secret:", "2fa:", "mfa:", "authenticator key:",
      };
            foreach (var pattern in totpKeyPatterns)
            {
                var idx = notes.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                var valueStart = idx + pattern.Length;
                while (valueStart < notes.Length && notes[valueStart] == ' ') valueStart++;
                var valueEnd = notes.IndexOfAny(new[] { '\n', '\r', ' ' }, valueStart);
                var value = (valueEnd < 0 ? notes[valueStart..] : notes[valueStart..valueEnd]).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    c.TotpRaw = NormaliseTotpValue(value);
                    if (c.TotpRaw != null) return;
                }
            }

            // 3. Bare Base32 secret anywhere in notes (? 16 chars, only A-Z 2-7 =)
            var words = notes.Split(new[] { ' ', '\t', '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var clean = word.TrimEnd('=').ToUpperInvariant();
                if (clean.Length >= 16 && IsBase32(clean))
                {
                    c.TotpRaw = NormaliseTotpValue(clean);
                    if (c.TotpRaw != null) return;
                }
            }

            // 4. Password field is an otpauth:// URI (rare Bitwarden edge case)
            if (!string.IsNullOrWhiteSpace(c.Password) &&
            c.Password.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                c.TotpRaw = NormaliseTotpValue(c.Password);
                c.Password = string.Empty;
            }
        }

        private static string? NormaliseTotpValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim();

            if (trimmed.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
                return trimmed.Contains("secret=", StringComparison.OrdinalIgnoreCase) ? trimmed : null;

            var normalised = trimmed.ToUpperInvariant().Replace(" ", "").Replace("-", "").TrimEnd('=');
            return normalised.Length >= 16 && IsBase32(normalised) ? normalised : null;
        }

        private static bool IsBase32(string s)
        {
            foreach (var ch in s.ToUpperInvariant())
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= '2' && ch <= '7') || ch == '='))
                    return false;
            return true;
        }

        // ── Authenticator file import ─────────────────────────────────────────
        public async Task<AuthenticatorImportPreview> PreviewAuthenticatorsAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("ImportService: PreviewAuthenticatorsAsync — {F}", Path.GetFileName(filePath));

            byte[]? rawBytes = null;
            try
            {
                rawBytes = await ReadFileSafeAsync(filePath, cancellationToken);
                var text = DecodeText(rawBytes);
                CryptographicOperations.ZeroMemory(rawBytes);
                rawBytes = null;

                var (format, candidates) = ParseAuthenticators(text, filePath);
                await MarkAuthenticatorDuplicatesAsync(candidates, cancellationToken);

                var warnings = new List<string>();
                if (format == AuthenticatorImportFormat.Unknown)
                    warnings.Add("File format not recognised. Only Aegis, Raivo, 2FAS JSON and otpauth:// text files are supported.");

                var dupes = candidates.Count(c => c.IsDuplicate);
                if (dupes > 0)
                    warnings.Add($"{dupes} account(s) already exist in your vault and will be skipped.");

                return new AuthenticatorImportPreview
                {
                    DetectedFormat = format,
                    TotalParsed = candidates.Count,
                    ValidCount = candidates.Count(c => !string.IsNullOrWhiteSpace(c.Secret)),
                    DuplicateCount = dupes,
                    Candidates = candidates.AsReadOnly(),
                    Warnings = warnings.AsReadOnly(),
                };
            }
            finally
            {
                if (rawBytes != null) CryptographicOperations.ZeroMemory(rawBytes);
                TryDeleteTempFile(filePath);
            }
        }

        public async Task<AuthenticatorImportResult> CommitAuthenticatorsAsync(
    AuthenticatorImportPreview preview,
          CancellationToken cancellationToken = default)
        {
            int imported = 0, skipped = 0, failed = 0;
            var errors = new List<string>();

            foreach (var c in preview.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (c.IsDuplicate) { skipped++; continue; }
                if (string.IsNullOrWhiteSpace(c.Secret)) { skipped++; continue; }
                try
                {
                    await SaveAuthenticatorCandidateAsync(c);
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ImportService: authenticator row {R} failed", c.SourceRowIndex);
                    errors.Add($"Row {c.SourceRowIndex}: {ex.Message}");
                    failed++;
                }
            }

            _logger.LogInformation(
  "ImportService: authenticator commit — imported={I} skipped={S} failed={F}",
 imported, skipped, failed);

            return new AuthenticatorImportResult
            {
                Imported = imported,
                Skipped = skipped,
                Failed = failed,
                Errors = errors.AsReadOnly(),
            };
        }

        /// <summary>
        /// Imports authenticators from raw URI strings.
        /// Handles both <c>otpauth://</c> (single account) and
        /// <c>otpauth-migration://offline?data=…</c> (Google Authenticator multi-account QR).
        /// </summary>
        public async Task<AuthenticatorImportResult> ImportAuthenticatorsFromUrisAsync(
  IEnumerable<string> uris,
     CancellationToken cancellationToken = default)
        {
            int imported = 0, skipped = 0, failed = 0;
            var errors = new List<string>();
            var existing = (await _storage.GetAuthenticatorsAsync()).ToList();
            int row = 0;

            foreach (var uri in uris)
            {
                cancellationToken.ThrowIfCancellationRequested();
                row++;
                if (string.IsNullOrWhiteSpace(uri)) continue;

                try
                {
                    // ── Google Authenticator migration QR ─────────────────────
                    // "Transfer accounts" produces a single otpauth-migration:// URI
                    // that encodes multiple accounts in a protobuf payload.
                    if (uri.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
                    {
                        var migCandidates = ParseGoogleMigrationUri(uri.Trim(), row);
                        foreach (var mc in migCandidates)
                        {
                            if (IsDuplicateAuthenticator(mc, existing)) { skipped++; continue; }
                            try
                            {
                                await SaveAuthenticatorCandidateAsync(mc);
                                imported++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "ImportService: migration entry {R} failed", mc.SourceRowIndex);
                                errors.Add($"Migration entry {mc.SourceRowIndex}: {ex.Message}");
                                failed++;
                            }
                        }
                        continue;
                    }

                    // ── Normal otpauth:// URI ─────────────────────────────────
                    var candidate = ParseOtpAuthUri(uri.Trim(), row);
                    if (candidate == null) { skipped++; continue; }
                    if (IsDuplicateAuthenticator(candidate, existing)) { skipped++; continue; }
                    await SaveAuthenticatorCandidateAsync(candidate);
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ImportService: URI row {R} failed", row);
                    errors.Add($"URI {row}: {ex.Message}");
                    failed++;
                }
            }

            return new AuthenticatorImportResult
            {
                Imported = imported,
                Skipped = skipped,
                Failed = failed,
                Errors = errors.AsReadOnly(),
            };
        }

        // ── Authenticator parsers ─────────────────────────────────────────────
        private (AuthenticatorImportFormat format, List<AuthenticatorCandidate> candidates)
            ParseAuthenticators(string text, string filePath)
        {
            var trimmed = text.TrimStart();
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".json" || trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                if (trimmed.Contains("\"aegis\"", StringComparison.OrdinalIgnoreCase) ||
   (trimmed.Contains("\"db\"", StringComparison.OrdinalIgnoreCase) &&
  trimmed.Contains("\"entries\"", StringComparison.OrdinalIgnoreCase)))
                    return (AuthenticatorImportFormat.AegisJson, ParseAegisJson(text));

                if (trimmed.Contains("\"services\"", StringComparison.OrdinalIgnoreCase))
                    return (AuthenticatorImportFormat.TwoFasJson, ParseTwoFasJson(text));

                if (trimmed.StartsWith('[') ||
                      (trimmed.Contains("\"issuer\"") && trimmed.Contains("\"secret\"")))
                    return (AuthenticatorImportFormat.RaivoJson, ParseRaivoJson(text));
            }

            if (text.Contains("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
                return (AuthenticatorImportFormat.GoogleMigration, ParseOtpAuthUriList(text));

            if (text.Contains("otpauth://", StringComparison.OrdinalIgnoreCase))
                return (AuthenticatorImportFormat.OtpAuthUriList, ParseOtpAuthUriList(text));

            return (AuthenticatorImportFormat.Unknown, new List<AuthenticatorCandidate>());
        }

        private List<AuthenticatorCandidate> ParseAegisJson(string text)
        {
            var result = new List<AuthenticatorCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                if (root.TryGetProperty("db", out var db) && db.ValueKind == JsonValueKind.String)
                {
                    _logger.LogWarning("ImportService: Aegis export is encrypted — cannot import");
                    return result;
                }

                var entries = root.TryGetProperty("db", out var dbObj) && dbObj.TryGetProperty("entries", out var ent)
                      ? ent
                : root.TryGetProperty("entries", out var ent2) ? ent2 : default;

                if (entries.ValueKind != JsonValueKind.Array) return result;

                int row = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    row++;
                    var type = entry.TryGetProperty("type", out var t) ? t.GetString() ?? "totp" : "totp";
                    var issuer = entry.TryGetProperty("issuer", out var i) ? i.GetString() ?? "" : "";
                    var name = entry.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!entry.TryGetProperty("info", out var info)) continue;

                    var secret = info.TryGetProperty("secret", out var s) ? s.GetString() ?? "" : "";
                    var algo = info.TryGetProperty("algo", out var a) ? a.GetString() ?? "SHA1" : "SHA1";
                    var digits = info.TryGetProperty("digits", out var d) ? d.GetInt32() : 6;
                    var period = info.TryGetProperty("period", out var p) ? p.GetInt32() : 30;

                    secret = NormaliseSecret(secret);
                    if (string.IsNullOrWhiteSpace(secret)) continue;

                    result.Add(new AuthenticatorCandidate
                    {
                        Issuer = issuer.Trim(),
                        Account = name.Trim(),
                        Secret = secret,
                        Algorithm = algo.ToUpperInvariant(),
                        Digits = digits,
                        Period = period,
                        Type = type.ToUpperInvariant(),
                        SourceRowIndex = row,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: Aegis JSON parse error");
            }
            return result;
        }

        private List<AuthenticatorCandidate> ParseTwoFasJson(string text)
        {
            var result = new List<AuthenticatorCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var services = root.TryGetProperty("services", out var s) ? s : root;
                int row = 0;

                foreach (var svc in services.EnumerateArray())
                {
                    row++;
                    var issuer = svc.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var otp = svc.TryGetProperty("otp", out var o) ? o : default;
                    if (otp.ValueKind == JsonValueKind.Undefined) continue;

                    var secret = otp.TryGetProperty("secret", out var sec) ? sec.GetString() ?? "" : "";
                    var algo = otp.TryGetProperty("algorithm", out var alg) ? alg.GetString() ?? "SHA1" : "SHA1";
                    var digits = otp.TryGetProperty("digits", out var dig) ? dig.GetInt32() : 6;
                    var period = otp.TryGetProperty("period", out var per) ? per.GetInt32() : 30;
                    var type = otp.TryGetProperty("tokenType", out var tt) ? tt.GetString() ?? "TOTP" : "TOTP";
                    var account = svc.TryGetProperty("login", out var lg) ? lg.GetString() ?? "" : "";

                    secret = NormaliseSecret(secret);
                    if (string.IsNullOrWhiteSpace(secret)) continue;

                    result.Add(new AuthenticatorCandidate
                    {
                        Issuer = issuer.Trim(),
                        Account = account.Trim(),
                        Secret = secret,
                        Algorithm = algo.ToUpperInvariant(),
                        Digits = digits,
                        Period = period,
                        Type = type.ToUpperInvariant(),
                        SourceRowIndex = row,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: 2FAS JSON parse error");
            }
            return result;
        }

        private List<AuthenticatorCandidate> ParseRaivoJson(string text)
        {
            var result = new List<AuthenticatorCandidate>();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var arr = root.ValueKind == JsonValueKind.Array
                  ? root
             : root.TryGetProperty("items", out var items) ? items : root;
                int row = 0;

                foreach (var item in arr.EnumerateArray())
                {
                    row++;
                    var issuer = item.TryGetProperty("issuer", out var i) ? i.GetString() ?? "" : "";
                    var account = item.TryGetProperty("account", out var a) ? a.GetString() ?? ""
                   : item.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
                    var secret = item.TryGetProperty("secret", out var s) ? s.GetString() ?? "" : "";
                    var algo = item.TryGetProperty("algorithm", out var alg) ? alg.GetString() ?? "SHA1" : "SHA1";
                    var digits = item.TryGetProperty("digits", out var d) ? d.GetInt32() : 6;
                    var period = item.TryGetProperty("timer", out var tm) ? tm.GetInt32()
        : item.TryGetProperty("period", out var p) ? p.GetInt32() : 30;

                    secret = NormaliseSecret(secret);
                    if (string.IsNullOrWhiteSpace(secret)) continue;

                    result.Add(new AuthenticatorCandidate
                    {
                        Issuer = issuer.Trim(),
                        Account = account.Trim(),
                        Secret = secret,
                        Algorithm = algo.ToUpperInvariant(),
                        Digits = digits,
                        Period = period,
                        Type = "TOTP",
                        SourceRowIndex = row,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: Raivo JSON parse error");
            }
            return result;
        }

        private List<AuthenticatorCandidate> ParseOtpAuthUriList(string text)
        {
            var result = new List<AuthenticatorCandidate>();
            int row = 0;

            foreach (var line in text.Split('\n'))
            {
                row++;
                var uri = line.Trim().TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(uri)) continue;

                // Expand Google Authenticator migration QRs inline
                if (uri.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
                {
                    result.AddRange(ParseGoogleMigrationUri(uri, row));
                    continue;
                }

                var c = ParseOtpAuthUri(uri, row);
                if (c != null) result.Add(c);
            }

            return result;
        }

        private AuthenticatorCandidate? ParseOtpAuthUri(string uri, int row)
        {
            if (!uri.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
                return null;
            try
            {
                var parsed = Uri.IsWellFormedUriString(uri, UriKind.Absolute)
                    ? new Uri(uri)
         : new Uri(uri.Replace(" ", "%20"));

                var type = parsed.Host.ToUpperInvariant();
                var label = Uri.UnescapeDataString(parsed.AbsolutePath.TrimStart('/'));
                string issuer = "", account = label;

                if (label.Contains(':'))
                {
                    var col = label.IndexOf(':');
                    issuer = label[..col].Trim();
                    account = label[(col + 1)..].Trim();
                }

                var query = System.Web.HttpUtility.ParseQueryString(parsed.Query);
                var secret = NormaliseSecret(query["secret"] ?? "");
                var issuerQ = query["issuer"] ?? "";
                var algorithm = (query["algorithm"] ?? "SHA1").ToUpperInvariant();
                var digitsStr = query["digits"];
                var periodStr = query["period"];

                if (!string.IsNullOrWhiteSpace(issuerQ)) issuer = issuerQ.Trim();
                if (string.IsNullOrWhiteSpace(issuer)) issuer = account;
                if (string.IsNullOrWhiteSpace(secret)) return null;

                return new AuthenticatorCandidate
                {
                    Issuer = issuer,
                    Account = account,
                    Secret = secret,
                    Algorithm = algorithm,
                    Digits = int.TryParse(digitsStr, out var d) ? d : 6,
                    Period = int.TryParse(periodStr, out var p) ? p : 30,
                    Type = type,
                    SourceRowIndex = row,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: could not parse URI at row {R}", row);
                return null;
            }
        }

        private static string NormaliseSecret(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.ToUpperInvariant().Replace(" ", "").Replace("-", "").TrimEnd('=');
            // Accept ≥ 8 Base32 chars (5 bytes / 40 bits minimum).
            // RFC 6238 recommends 20 bytes but Google Authenticator migration
            // exports regularly contain 8–10 byte secrets.
            return IsBase32(s) && s.Length >= 8 ? s : string.Empty;
        }

        // ── Google Authenticator migration QR decoder ─────────────────────────
        //
        // Google Authenticator's "Transfer accounts" screen produces a QR code whose
        // content is:  otpauth-migration://offline?data=<base64url-encoded-protobuf>
        //
        // MigrationPayload proto schema (relevant field numbers only):
        //   message OtpParameters {          // field 1, repeated
        //     bytes  secret  = 1;
        //     string name       = 2;         // "issuer:account" or "account"
        //     string issuer     = 3;
        //     int32  algorithm  = 4;         // 1=SHA1  2=SHA256  3=SHA512  4=MD5
        //     int32  digit_count= 5;         // 1=SIX   2=EIGHT
        //     int32  type       = 6; // 1=HOTP  2=TOTP
        //     int64  counter    = 7;
        //   }
        //   message MigrationPayload { repeated OtpParameters otp_parameters = 1; ... }
        //
        // Self-contained minimal protobuf reader — Google.Protobuf NuGet not required.

        private List<AuthenticatorCandidate> ParseGoogleMigrationUri(string uri, int baseRow)
        {
            var result = new List<AuthenticatorCandidate>();
            try
            {
                var dataIdx = uri.IndexOf("?data=", StringComparison.OrdinalIgnoreCase);
                if (dataIdx < 0)
                {
                    _logger.LogWarning("ImportService: Google migration URI missing ?data= parameter");
                    return result;
                }

                // Extract data value — stop at '&' (extra query params) or end-of-string,
                // then strip null bytes that some ZXing builds append on Android.
                var dataRaw = uri[(dataIdx + 6)..];
                var ampIdx = dataRaw.IndexOf('&');
                if (ampIdx >= 0) dataRaw = dataRaw[..ampIdx];

                // URL-decode ? base64url ? base64 ? add padding
                var b64 = Uri.UnescapeDataString(dataRaw)
                   .TrimEnd('\0', '\r', '\n')
                   .Replace('-', '+')
              .Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');

                var payload = Convert.FromBase64String(b64);
                var entries = DecodeMigrationPayload(payload);

                int subRow = 0;
                foreach (var entry in entries)
                {
                    subRow++;
                    var secret = NormaliseSecret(entry.Secret);
                    if (string.IsNullOrWhiteSpace(secret)) continue;

                    result.Add(new AuthenticatorCandidate
                    {
                        Issuer = entry.Issuer.Trim(),
                        Account = entry.Account.Trim(),
                        Secret = secret,
                        Algorithm = entry.Algorithm,
                        Digits = entry.Digits,
                        Period = 30,
                        Type = entry.Type,
                        SourceRowIndex = baseRow * 1000 + subRow,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: Google migration URI parse error");
            }
            return result;
        }

        // ── Minimal protobuf reader ───────────────────────────────────────────
        private static List<MigrationOtpEntry> DecodeMigrationPayload(byte[] data)
        {
            var result = new List<MigrationOtpEntry>();
            int pos = 0;

            while (pos < data.Length)
            {
                var tag = (int)ReadVarint(data, ref pos);
                int fieldNum = tag >> 3;
                int wireType = tag & 0x7;

                switch (wireType)
                {
                    case 0: // varint — version, batch_size, batch_index, etc.
                        ReadVarint(data, ref pos);
                        break;
                    case 1: // 64-bit fixed — skip
                        pos += 8;
                        break;
                    case 2: // length-delimited
                        {
                            var len = (int)ReadVarint(data, ref pos);
                            var chunk = data[pos..(pos + len)];
                            pos += len;
                            if (fieldNum == 1) // repeated OtpParameters
                                result.Add(DecodeOtpParameters(chunk));
                            break;
                        }
                    case 5: // 32-bit fixed — skip
                        pos += 4;
                        break;
                    default:
                        pos = data.Length; // unknown wire type — stop safely
                        break;
                }
            }

            return result;
        }

        private static MigrationOtpEntry DecodeOtpParameters(byte[] data)
        {
            var e = new MigrationOtpEntry();
            int pos = 0;

            while (pos < data.Length)
            {
                var tag = (int)ReadVarint(data, ref pos);
                int fieldNum = tag >> 3;
                int wireType = tag & 0x7;

                switch (wireType)
                {
                    case 0:
                        {
                            var val = (int)ReadVarint(data, ref pos);
                            switch (fieldNum)
                            {
                                case 4: e.Algorithm = val switch { 2 => "SHA256", 3 => "SHA512", _ => "SHA1" }; break;
                                case 5: e.Digits = val == 2 ? 8 : 6; break;
                                case 6: e.Type = val == 1 ? "HOTP" : "TOTP"; break;
                            }
                            break;
                        }
                    case 1:
                        pos += 8;
                        break;
                    case 2:
                        {
                            var len = (int)ReadVarint(data, ref pos);
                            var chunk = data.AsSpan(pos, len);
                            pos += len;
                            switch (fieldNum)
                            {
                                case 1:
                                    e.Secret = Base32Encode(chunk);
                                    break;
                                case 2:
                                    {
                                        var name = Encoding.UTF8.GetString(chunk);
                                        var colon = name.IndexOf(':');
                                        if (colon > 0)
                                        {
                                            if (string.IsNullOrWhiteSpace(e.Issuer))
                                                e.Issuer = name[..colon].Trim();
                                            e.Account = name[(colon + 1)..].Trim();
                                        }
                                        else
                                        {
                                            e.Account = name.Trim();
                                        }
                                        break;
                                    }
                                case 3:
                                    e.Issuer = Encoding.UTF8.GetString(chunk).Trim();
                                    break;
                            }
                            break;
                        }
                    case 5:
                        pos += 4;
                        break;
                    default:
                        pos = data.Length;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(e.Issuer))
                e.Issuer = e.Account;

            return e;
        }

        private static ulong ReadVarint(byte[] data, ref int pos)
        {
            ulong result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                var b = data[pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        private static string Base32Encode(ReadOnlySpan<byte> data)
        {
            const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            if (data.IsEmpty) return string.Empty;
            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buf = 0, bits = 0;
            foreach (var b in data)
            {
                buf = (buf << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    sb.Append(alpha[(buf >> bits) & 0x1F]);
                }
            }
            if (bits > 0) sb.Append(alpha[(buf << (5 - bits)) & 0x1F]);
            return sb.ToString();
        }

        private sealed class MigrationOtpEntry
        {
            public string Issuer { get; set; } = string.Empty;
            public string Account { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
            public string Algorithm { get; set; } = "SHA1";
            public int Digits { get; set; } = 6;
            public string Type { get; set; } = "TOTP";
        }

        private async Task MarkAuthenticatorDuplicatesAsync(
        List<AuthenticatorCandidate> candidates,
        CancellationToken ct)
        {
            var existing = (await _storage.GetAuthenticatorsAsync()).ToList();
            foreach (var c in candidates)
            {
                ct.ThrowIfCancellationRequested();
                c.IsDuplicate = IsDuplicateAuthenticator(c, existing);
            }
        }

        private static bool IsDuplicateAuthenticator(
               AuthenticatorCandidate c, IList<Core.Models.Authenticator> existing) =>
               existing.Any(e =>
                   string.Equals(e.Issuer ?? "", c.Issuer, StringComparison.OrdinalIgnoreCase) &&
         string.Equals(e.Username ?? "", c.Account, StringComparison.OrdinalIgnoreCase));

        private async Task SaveAuthenticatorCandidateAsync(AuthenticatorCandidate c)
        {
            var encSecret = await EncryptAsync(c.Secret);
            var authType = c.Type switch { "HOTP" => Core.Models.AuthenticatorType.Hotp, _ => Core.Models.AuthenticatorType.Totp };
            var hashAlgo = c.Algorithm switch
            {
                "SHA256" => Core.Models.HashAlgorithm.Sha256,
                "SHA512" => Core.Models.HashAlgorithm.Sha512,
                _ => Core.Models.HashAlgorithm.Sha1,
            };

            await _storage.AddAuthenticatorAsync(new  Authenticator
            {
                Id = Guid.NewGuid(),
                Issuer = c.Issuer,
                Username = c.Account,
                Secret = encSecret,
                Type = authType,
                Algorithm = hashAlgo,
                Digits = c.Digits,
                Period = c.Period,
            });
        }

        public async Task<IEnumerable<ImportAuditRecord>> GetImportHistoryAsync()
        {
            var all = await _storage.GetImportAuditsAsync();
            return all.OrderByDescending(r => r.OccurredAt);
        }

        private async Task SaveAuditAsync(
 ImportPreview preview, DuplicateMergeOption mergeOption,
        int imported, int skipped, int replaced, int failed)
        {
            try
            {
                await _storage.SaveImportAuditAsync(new ImportAuditRecord
                {
                    Format = preview.DetectedFormat,
                    TotalParsed = preview.TotalRows,
                    Imported = imported,
                    Skipped = skipped,
                    Replaced = replaced,
                    Failed = failed,
                    WeakPasswords = preview.WeakCount,
                    ReusedPasswords = preview.ReusedCount,
                    Duplicates = preview.DuplicateCount,
                    TotpEntries = preview.TotpCount,
                    MergeOption = mergeOption,
                    WasCommitted = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: could not write audit record");
            }
        }

        private static List<string> BuildWarnings(List<ImportCandidate> candidates, ImportSourceFormat format)
        {
            var w = new List<string>();

            if (format is ImportSourceFormat.Unknown or ImportSourceFormat.GenericCSV)
                w.Add("Format not fully recognised — column mapping is approximate. Review the preview carefully.");

            var weak = candidates.Count(c => c.IsWeak);
            var reused = candidates.Count(c => c.IsReused);
            var dupes = candidates.Count(c => c.IsDuplicate);
            var totp = candidates.Count(c => c.HasTotp);
            var extra = candidates.Count(c => c.ImportedFields.Count > 0);

            if (weak > 0) w.Add($"{weak} password{Pl(weak)} are weak and should be updated after import.");
            if (reused > 0) w.Add($"{reused} item{Pl(reused)} share the same password — consider making each unique.");
            if (dupes > 0) w.Add($"{dupes} item{Pl(dupes)} already exist in your vault. Choose a merge option below.");
            if (totp > 0) w.Add($"{totp} item{Pl(totp)} include a TOTP authenticator secret and will be imported with 2FA.");
            if (extra > 0) w.Add($"{extra} item{Pl(extra)} contain extra fields not recognised by Fortress — they have been preserved as encrypted imported data.");

            return w;
        }

        private static string Pl(int n) => n == 1 ? "" : "s";

        internal static List<string[]> ParseAllCsvRows(string text)
        {
            var rows = new List<string[]>(256);
            var fields = new List<string>(16);
            var current = new StringBuilder(128);
            bool inQuote = false;

            int start = (text.Length > 0 && text[0] == '\uFEFF') ? 1 : 0;

            for (int i = start; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuote)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuote = false;
                    }
                    else current.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuote = true;
                    else if (ch == ',') { fields.Add(current.ToString()); current.Clear(); }
                    else if (ch == '\n')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                        if (!(fields.Count == 1 && fields[0] == ""))
                            rows.Add(fields.ToArray());
                        fields.Clear();
                    }
                    else if (ch == '\r') { /* skip */ }
                    else current.Append(ch);
                }
            }

            if (current.Length > 0 || fields.Count > 0)
            {
                fields.Add(current.ToString());
                if (!(fields.Count == 1 && fields[0] == ""))
                    rows.Add(fields.ToArray());
            }

            return rows;
        }

        private static string GetCol(string[] cols, Dictionary<string, int> map, string key)
        {
            if (!map.TryGetValue(key, out int idx)) return string.Empty;
            return idx < cols.Length ? cols[idx].Trim() : string.Empty;
        }

        internal static string DecodeText(byte[] bytes)
        {
            if (bytes.Length == 0) return string.Empty;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4);
            try
            {
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                return utf8.GetString(bytes);
            }
            catch (DecoderFallbackException) { }
            return Encoding.Latin1.GetString(bytes);
        }

        private void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path) &&
                          path.StartsWith(FileSystem.CacheDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    var size = new FileInfo(path).Length;
                    if (size > 0 && size < 64 * 1024 * 1024)
                    {
                        using var f = File.OpenWrite(path);
                        var zeros = ArrayPool<byte>.Shared.Rent(4096);
                        try
                        {
                            long remaining = size;
                            while (remaining > 0)
                            {
                                var chunk = (int)Math.Min(zeros.Length, remaining);
                                f.Write(zeros, 0, chunk);
                                remaining -= chunk;
                            }
                        }
                        finally { ArrayPool<byte>.Shared.Return(zeros); }
                    }
                    File.Delete(path);
                    _logger.LogDebug("ImportService: temp file securely deleted");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportService: could not delete temp file");
            }
        }

        private static void MarkReusedPasswords(List<ImportCandidate> candidates, ImportHmacProvider hmac)
        {
            var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c.Password)) continue;
                var tag = hmac.ComputeTag(c.Password);
                tagCounts[tag] = tagCounts.TryGetValue(tag, out var n) ? n + 1 : 1;
            }
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c.Password)) continue;
                var tag = hmac.ComputeTag(c.Password);
                c.IsReused = tagCounts.TryGetValue(tag, out var count) && count > 1;
            }
        }

        private async Task MarkDuplicatesAsync(List<ImportCandidate> candidates, CancellationToken ct)
        {
            var existing = (await _storage.GetLoginItemsAsync()).ToList();
            var byDomain = new Dictionary<string, List<LoginItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in existing)
            {
                var rd = string.IsNullOrWhiteSpace(item.Url)
                      ? string.Empty
             : DomainCanonicaliser.ComputeRegistrableDomain(
               DomainCanonicaliser.NormaliseHost(TryGetHost(item.Url)));
                if (string.IsNullOrWhiteSpace(rd)) continue;
                if (!byDomain.TryGetValue(rd, out var list))
                    byDomain[rd] = list = new List<LoginItem>();
                list.Add(item);
            }

            foreach (var c in candidates)
            {
                ct.ThrowIfCancellationRequested();
                var primaryRd = c.Urls.Count > 0 ? c.Urls[0].RegistrableDomain : string.Empty;

                if (string.IsNullOrWhiteSpace(primaryRd))
                {
                    var titleMatch = existing.FirstOrDefault(e =>
                        !string.IsNullOrWhiteSpace(c.Label) &&
                string.Equals(e.Label, c.Label, StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(e.Username ?? "", c.Username, StringComparison.OrdinalIgnoreCase));
                    if (titleMatch != null)
                    {
                        c.IsDuplicate = true;
                        c.DuplicateOfId = titleMatch.Id;
                        c.DuplicateReason = $"Same title and username as existing item \"{titleMatch.Label}\"";
                    }
                    continue;
                }

                if (!byDomain.TryGetValue(primaryRd, out var domainItems)) continue;

                LoginItem? match = string.IsNullOrWhiteSpace(c.Username)
                    ? domainItems.FirstOrDefault()
                   : domainItems.FirstOrDefault(e =>
                  string.Equals(e.Username ?? "", c.Username, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    c.IsDuplicate = true;
                    c.DuplicateOfId = match.Id;
                    c.DuplicateReason = string.IsNullOrWhiteSpace(c.Username)
     ? $"Domain \"{primaryRd}\" already exists in vault"
       : $"Domain \"{primaryRd}\" + username \"{c.Username}\" already exists in vault";
                }
            }
        }

        private static string TryGetHost(string rawUrl)
        {
            try
            {
                var s = rawUrl.Trim();
                if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    s = "https://" + s;
                return new Uri(s).Host;
            }
            catch { return rawUrl.Trim(); }
        }

        private async Task SaveLoginItemAsync(ImportCandidate candidate, Guid? existingId)
        {
            var encPassword = await EncryptAsync(candidate.Password);
            var encOtp = candidate.HasTotp ? await EncryptAsync(candidate.TotpRaw!) : string.Empty;

            var extras = new Dictionary<string, object>();
            if (candidate.Urls.Count > 1)
                extras["additionalUrls"] = candidate.Urls.Skip(1)
                          .Select(u => u.StorageUrl)
                          .Where(u => !string.IsNullOrWhiteSpace(u))
                  .ToList();
            if (candidate.ImportedFields.Count > 0)
                extras["importedFields"] = candidate.ImportedFields;

            string? extraJson = extras.Count > 0 ? JsonSerializer.Serialize(extras) : null;

            var notes = string.IsNullOrWhiteSpace(extraJson)
                 ? candidate.Notes
         : string.IsNullOrWhiteSpace(candidate.Notes)
                    ? extraJson
              : candidate.Notes + "\n" + extraJson;

            await _storage.SaveLoginItemAsync(new Core.Models.LoginItem
            {
                Id = existingId ?? Guid.NewGuid(),
                Label = candidate.Label,
                Username = candidate.Username,
                Password = encPassword,
                Url = candidate.PrimaryUrl.StorageUrl,
                Notes = notes,
                OtpSecret = encOtp,
                PasswordStrengthScore = candidate.PasswordStrengthScore,
                PasswordStrengthLevel = (int)candidate.PasswordStrengthLevel,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        private async Task<string> EncryptAsync(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var result = await _crypto.Encrypt(plainText);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Encryption failed: {result.ErrorMessage}");
            return result.Data ?? string.Empty;
        }

        /// <inheritdoc/>
        public async Task<AuthenticatorImportPreview> ImportAuthenticatorsPreviewFromUrisAsync(
            IEnumerable<string> uris,
  CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("ImportService: ImportAuthenticatorsPreviewFromUrisAsync");
            var tmp = Path.Combine(FileSystem.CacheDirectory, $"qrimport_{Guid.NewGuid():N}.txt");
            try
            {
                await File.WriteAllLinesAsync(tmp, uris, cancellationToken);
                return await PreviewAuthenticatorsAsync(tmp, cancellationToken);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                throw;
            }
        }
    }
}

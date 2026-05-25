namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Offline URL canonicalisation used by the import engine and the autofill
    /// suggestion engine.
    ///
    /// Design decisions
    /// ────────────────
    /// – No external PSL (Public Suffix List) dependency – ships a curated
    ///   suffix table that covers > 99 % of passwords-manager content.
    /// – Thread-safe: all state is static / read-only.
    /// – Never logs the raw URL to protect any user-supplied path/query secrets.
    ///
    /// Algorithm for eTLD+1 (registrable domain)
    /// ──────────────────────────────────────────
    ///   1. Normalise the host to lowercase, strip leading "www.".
    ///   2. Walk label-by-label from the right.  If the rightmost N labels form
    ///      a known multi-part TLD (e.g. "co.uk", "com.au", "org.uk") take
    ///      those N labels + one more to the left as the registrable domain.
    ///   3. Otherwise use the last two labels (standard "example.com").
    ///   4. Special-case: ccTLD-only bare labels ("localhost", IP addresses)
    ///      return the host as-is.
    /// </summary>
    public static class DomainCanonicaliser
    {
        // ── Curated multi-part eTLDs ──────────────────────────────────────────
        // Covers the most common ccSLD patterns found in password-manager exports.
        // Deliberately NOT a full PSL – keeps the binary lean on mobile.
    private static readonly HashSet<string> _multiPartTlds =
          new(StringComparer.OrdinalIgnoreCase)
   {
   // UK
         "co.uk", "org.uk", "me.uk", "net.uk", "ltd.uk", "plc.uk",
        "gov.uk", "mod.uk", "nhs.uk", "police.uk", "sch.uk",
     // Australia
            "com.au", "net.au", "org.au", "id.au", "gov.au", "edu.au",
   // New Zealand
   "co.nz", "net.nz", "org.nz", "govt.nz", "ac.nz",
            // Japan
            "co.jp", "or.jp", "ne.jp", "ac.jp", "go.jp", "ed.jp",
            // India
       "co.in", "net.in", "org.in", "gov.in", "ac.in", "nic.in",
          // Brazil
"com.br", "net.br", "org.br", "gov.br", "edu.br",
      // South Africa
 "co.za", "net.za", "org.za", "gov.za",
          // Germany (some registrars use these)
            "com.de",
// South Korea
            "co.kr", "or.kr", "ne.kr", "go.kr", "re.kr",
    // China
   "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn",
        // Spain
            "com.es", "nom.es", "org.es", "gob.es",
 // Argentina
            "com.ar", "net.ar", "org.ar", "gov.ar",
 // Mexico
 "com.mx", "net.mx", "org.mx", "gob.mx",
  // Hong Kong
  "com.hk", "net.hk", "org.hk", "gov.hk",
     // Singapore
    "com.sg", "net.sg", "org.sg", "gov.sg", "edu.sg",
   // Belgium
            "ac.be",
            // France
 "asso.fr", "nom.fr",
            // Italy
     "co.it",
   // Netherlands
  "co.nl",
  };

        // Schemes that are definitely not web URLs – skip parsing
 private static readonly HashSet<string> _appSchemes =
  new(StringComparer.OrdinalIgnoreCase)
        {
    "androidapp", "iosapp", "android", "ios",
   };

        // ── Public API ────────────────────────────────────────────────────────
    /// <summary>
        /// Canonicalise a raw URL string from an import file.
   /// Always returns a non-null <see cref="CanonicalUrl"/>; an empty input
        /// produces an empty result rather than throwing.
     /// </summary>
     public static Models.CanonicalUrl Canonicalise(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
    return new Models.CanonicalUrl { OriginalUrl = string.Empty };

            var trimmed = raw.Trim();

            // ── 1. App-scheme URIs (androidapp://, iosapp://) ─────────────────
       var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd > 0)
            {
      var scheme = trimmed[..schemeEnd];
       if (_appSchemes.Contains(scheme))
                {
     return new Models.CanonicalUrl
        {
    OriginalUrl      = trimmed,
     Host= trimmed,   // store whole URI as "host"
        RegistrableDomain = trimmed,
             StorageUrl       = trimmed,
           };
             }
   }

// ── 2. Ensure parseable scheme ────────────────────────────────────
    var toparse = trimmed;
            if (!toparse.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
    !toparse.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
 toparse = "https://" + toparse;
            }

   Uri? uri;
       try { uri = new Uri(toparse, UriKind.Absolute); }
            catch
            {
    // Cannot parse – store raw; no host/registrable info
              return new Models.CanonicalUrl
    {
        OriginalUrl       = trimmed,
       StorageUrl        = trimmed,
       };
}

        // ── 3. Normalise host ─────────────────────────────────────────────
       var host = NormaliseHost(uri.Host);

            // ── 4. Compute registrable domain (eTLD+1) ────────────────────────
         var registrable = ComputeRegistrableDomain(host);

 // ── 5. Build clean storage URL ────────────────────────────────────
     // Remove userinfo, fragment, and normalise scheme to https where
   // it was already https or was missing (we inserted https:// above).
            // Preserve http:// for sites that genuinely serve plain HTTP.
 var scheme2 = string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                ? "http"
        : "https";

            string storageUrl;
 try
            {
     var builder = new UriBuilder(scheme2, host)
             {
    Path  = uri.AbsolutePath == "/" ? "" : uri.AbsolutePath,
           Query = string.Empty,
              };
       storageUrl = builder.Uri.ToString().TrimEnd('/');
            }
            catch
   {
          storageUrl = $"{scheme2}://{host}";
            }

     return new Models.CanonicalUrl
            {
   OriginalUrl  = trimmed,
   Host   = host,
    RegistrableDomain = registrable,
                StorageUrl        = storageUrl,
 };
        }

/// <summary>
        /// Returns the registrable domain (eTLD+1) from an already-normalised host.
        /// Exported for use in duplicate-detection and autofill matching.
    /// </summary>
        public static string ComputeRegistrableDomain(string normalisedHost)
    {
        if (string.IsNullOrWhiteSpace(normalisedHost)) return string.Empty;

          // IP addresses – return as-is
            if (IsIpAddress(normalisedHost)) return normalisedHost;

            var labels = normalisedHost.Split('.');
 if (labels.Length < 2) return normalisedHost;   // "localhost" etc.

            // Check two-label suffix (e.g. "co.uk")
      if (labels.Length >= 3)
        {
         var twoLabel = $"{labels[^2]}.{labels[^1]}";
     if (_multiPartTlds.Contains(twoLabel))
 {
 // Need eTLD+1 = labels[-3].twoLabel
          return $"{labels[^3]}.{twoLabel}";
    }
      }

         // Standard: last two labels
            return $"{labels[^2]}.{labels[^1]}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        /// <summary>Lowercase host, strip leading "www.".</summary>
    public static string NormaliseHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
   var lower = host.Trim().ToLowerInvariant();
            return lower.StartsWith("www.") ? lower[4..] : lower;
        }

        private static bool IsIpAddress(string host)
        {
   return System.Net.IPAddress.TryParse(host, out _);
        }
    }
}

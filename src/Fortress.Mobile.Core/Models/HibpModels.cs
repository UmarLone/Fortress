namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// Result of a Have I Been Pwned breach lookup for one email address.
    /// The email itself is never sent to the API – the k-anonymity model
    /// hashes the email with SHA-1 and only sends the first 5 hex characters.
    /// </summary>
    public sealed class HibpEmailResult
    {
        /// <summary>The email address that was checked (stored locally only, never sent).</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>True when the email appears in at least one known breach.</summary>
        public bool IsBreached { get; init; }

    /// <summary>Number of distinct breaches this email appears in.</summary>
        public int BreachCount { get; init; }

        /// <summary>Names of breaches this email appeared in (from HIBP API).</summary>
        public IReadOnlyList<string> BreachNames { get; init; } = Array.Empty<string>();

        /// <summary>When this result was fetched (UTC).</summary>
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
  /// Result of a Have I Been Pwned password check using the k-anonymity API.
    /// Only the first 5 characters of the SHA-1 hash are ever sent to the network.
    /// </summary>
    public sealed class HibpPasswordResult
    {
        /// <summary>Number of times this password appears in HIBP breach data. 0 = not found.</summary>
        public int PwnCount { get; init; }

        public bool IsPwned => PwnCount > 0;
    }
}

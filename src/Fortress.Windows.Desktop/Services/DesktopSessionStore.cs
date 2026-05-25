namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Holds the in-memory session token issued by Fortress.Service after a
/// successful unlock.  Shared between <see cref="VaultSessionService"/>
    /// (which sets it) and <see cref="PipeBackedDataService"/> (which reads it).
    /// Registered as a singleton in the DI container.
    /// </summary>
    public interface IDesktopSessionStore
    {
    /// <summary>
        /// The opaque session token returned by Fortress.Service on UnlockWithPassword
        /// or UnlockWithPin.  Null when the vault is locked.
     /// </summary>
        string? SessionToken { get; }

        /// <summary>True while a non-null token is held.</summary>
        bool IsUnlocked { get; }

        /// <summary>Stores the token after a successful service unlock.</summary>
   void SetToken(string token);

        /// <summary>Clears the token (vault locked).</summary>
      void ClearToken();
    }

  /// <summary>Thread-safe singleton implementation.</summary>
  public sealed class DesktopSessionStore : IDesktopSessionStore
    {
        private volatile string? _token;

        public string? SessionToken => _token;
   public bool IsUnlocked => _token is not null;

        public void SetToken(string token)  => _token = token;
     public void ClearToken()            => _token = null;
    }
}

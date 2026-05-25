namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// A user-defined group (folder) that credentials can be assigned to.
    /// Stored as a lightweight record – no encryption needed (name is not sensitive).
  /// </summary>
    public class VaultGroup
    {
        public Guid   Id          { get; set; } = Guid.NewGuid();
     public string Name        { get; set; } = string.Empty;
    /// <summary>Material icon glyph key – e.g. "Bank", "Work", "Shopping".</summary>
        public string IconKey     { get; set; } = "Folder";
     /// <summary>Hex colour string for the group chip – e.g. "#3B82F6".</summary>
        public string Color  { get; set; } = "#3B82F6";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

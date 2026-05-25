namespace Fortress.Core.Models
{
    public class VaultGroup
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
      public string IconKey { get; set; } = "Folder";
      public string Color { get; set; } = "#3B82F6";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class VaultGroupMembership
    {
public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }
   public Guid CredentialId { get; set; }
    }
}

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// Junction record linking a <see cref="VaultGroup"/> to a <see cref="Credential"/>.
    /// Stored in its own LiteDB collection so group membership can be changed
    /// without touching the credential documents themselves.
    /// </summary>
    public class VaultGroupMembership
    {
        public Guid Id           { get; set; } = Guid.NewGuid();
        public Guid GroupId      { get; set; }
        public Guid CredentialId { get; set; }
    }
}

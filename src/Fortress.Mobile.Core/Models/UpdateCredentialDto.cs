using System;

namespace Fortress.Mobile.Core.Models
{
    public class UpdateCredentialDto
    {
        public Guid Id { get; set; }
        public CredentialType CredentialType { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string Data { get; set; }
        public string Meta { get; set; }
        public string ParentCredentialId { get; set; }
    }
}

using System;

namespace Fortress.Mobile.Core.Models
{
    public class VerifyIdentityResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }
}

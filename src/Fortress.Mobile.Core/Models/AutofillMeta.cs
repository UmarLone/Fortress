using System.Text.Json.Serialization;

namespace Fortress.Mobile.Core.Models
{
  /// <summary>
    /// Serialised into <see cref="CredentialView.Meta"/> for credit-card credentials
    /// so the Android autofill builder can write card fields into payment forms.
    /// </summary>
    public sealed class CardAutofillMeta
    {
        [JsonPropertyName("number")]
public string Number { get; set; } = string.Empty;

        [JsonPropertyName("cardholderName")]
     public string CardholderName { get; set; } = string.Empty;

     [JsonPropertyName("expMonth")]
        public string ExpMonth { get; set; } = string.Empty;

     [JsonPropertyName("expYear")]
     public string ExpYear { get; set; } = string.Empty;

        [JsonPropertyName("cvv")]
 public string Cvv { get; set; } = string.Empty;

        [JsonPropertyName("network")]
    public string Network { get; set; } = string.Empty;
    }

    /// <summary>
    /// Serialised into <see cref="CredentialView.Meta"/> for identity credentials
    /// so the Android autofill builder can populate address / contact forms.
    /// </summary>
    public sealed class IdentityAutofillMeta
{
  [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("middleName")]
        public string MiddleName { get; set; } = string.Empty;

   [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
  public string Phone { get; set; } = string.Empty;

 [JsonPropertyName("address")]
  public string Address { get; set; } = string.Empty;

        [JsonPropertyName("address2")]
        public string Address2 { get; set; } = string.Empty;

        [JsonPropertyName("city")]
     public string City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")]
     public string PostalCode { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
    }
}

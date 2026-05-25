using Fortress.Mobile.Core.Models;
using Prism.Events;

namespace Fortress.Mobile.Core.EventAggregators
{
    public class RequestingApplication
    {
        public string Package { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsFillContext { get; set; }
        public bool IsAddOrSaveContext { get; set; }
        public FillRequestType FillRequestType { get; set; }
        /// <summary>Card, Login or Identity — used to route add-new/save to the correct page.</summary>
        public CipherType FillType { get; set; } = CipherType.Login;

        // ── Card save fields (populated by OnSaveRequest for CipherType.Card) ──
        public string CardName     { get; set; } = string.Empty;
        public string CardNumber   { get; set; } = string.Empty;
        public string CardExpMonth { get; set; } = string.Empty;
        public string CardExpYear  { get; set; } = string.Empty;
        public string CardCode     { get; set; } = string.Empty;
    }
}

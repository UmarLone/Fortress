namespace Fortress.Mobile.Core.Models
{
    public class TokenValidation
    {
        public string Key { get; set; }
        public string SerialNumber { get; set; }
        public string DeviceType { get; set; }
        public bool IsValid { get; set; }
        public string MacAddress { get; set; }
        public bool ValidationFinished { get; set; }
        public bool IsActive { get; set; }
    }
}

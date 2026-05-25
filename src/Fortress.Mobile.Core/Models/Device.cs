using System;

namespace Fortress.Mobile.Core.Models
{
    public class Device
    {

        public Guid Id { get; set; }
        public string SerialNumber { get; set; }
        public string DeviceType { get; set; }
        public string MacAddress { get; set; }
        public string Pin { get; set; }
    }
}

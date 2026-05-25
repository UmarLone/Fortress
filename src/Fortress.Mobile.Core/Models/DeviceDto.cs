using Prism.Mvvm;
using System;

namespace Fortress.Mobile.Core.Models
{
    public class DeviceDto : BindableBase
    {
        private Guid id;
        public Guid Id
        {
            get { return id; }
            set { SetProperty(ref id, value); }
        }
        private string serialNumber;
        public string SerialNumber
        {
            get { return !string.IsNullOrEmpty(serialNumber) ? serialNumber.Trim() : serialNumber; }
            set { SetProperty(ref serialNumber, !string.IsNullOrEmpty(value) ? value.Trim() : value); }
        }
        private string iconUri;
        public string IconUri
        {
            get { return iconUri; }
            set { SetProperty(ref iconUri, value); }
        }
        private string deviceType;
        public string DeviceType
        {
            get { return deviceType; }
            set { SetProperty(ref deviceType, value); }
        }
        private string macAddress;
        public string MacAddress
        {
            get { return macAddress; }
            set { SetProperty(ref macAddress, value); }
        }

        private string pin;
        public string Pin
        {
            get { return pin; }
            set { SetProperty(ref pin, value); }
        }
        private string confirmPin;
        public string ConfirmPin
        {
            get { return confirmPin; }
            set { SetProperty(ref confirmPin, value); }
        }
        private bool isForcedReset;
        public bool IsForcedReset
        {
            get { return isForcedReset; }
            set { SetProperty(ref isForcedReset, value); }
        }
        
    }
}

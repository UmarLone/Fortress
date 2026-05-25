using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Prism.Mvvm;
using System.Collections.Generic;

namespace Fortress.Mobile.Core.Models
{
    public class MetaView : BindableBase
    {
        private string notes;
        public string Notes
        {
            get { return notes; }
            set { SetProperty(ref notes, value); }
        }
        private bool disableAutofill;
        public bool DisableAutofill
        {
            get { return disableAutofill; }
            set { SetProperty(ref disableAutofill, value); }
        }
        private bool defaultPassword;
        public bool DefaultPassword
        {
            get { return defaultPassword; }
            set { SetProperty(ref defaultPassword, value); }
        }

        private IDictionary<string, JToken> additonalData;
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData
        {
            get { return additonalData; }
            set { SetProperty(ref additonalData, value); }
        }
    }
}

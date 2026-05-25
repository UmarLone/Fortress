using Prism.Mvvm;

namespace Fortress.Mobile.Core.Models
{
    public class CreditCardView : BindableBase
    {
        private string name;
        public string Name
        {
            get { return name; }
            set { SetProperty(ref name, value); }
        }
        private string number;
        public string Number
        {
            get { return number; }
            set { SetProperty(ref number, value); }
        }
        private string cvv;
        public string Cvv
        {
            get { return cvv; }
            set { SetProperty(ref cvv, value); }
        }
        private string expiry;
        public string Expiry
        {
            get { return expiry; }
            set { SetProperty(ref expiry, value); }
        }
    }
}

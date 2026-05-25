namespace Fortress.Models
{
    public class FailedAttemptsOption : BindableBase
    {
        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private int _key;
        public int Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }
    }
}

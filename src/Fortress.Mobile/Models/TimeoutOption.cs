namespace Fortress.Models
{
    public   class TimeoutOption : BindableBase
    {
        private string _value;
        public  string Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }
        private int _key;
        public int Key
        {
            get { return _key; }
            set { SetProperty(ref _key, value); }
        }
    }
}

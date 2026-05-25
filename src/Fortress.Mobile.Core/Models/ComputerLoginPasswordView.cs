using Prism.Mvvm;

namespace Fortress.Mobile.Core.Models
{
    public class ComputerLoginPasswordView : BindableBase
    {
        private int loginType = (int)CredentialType.Application;
        public int LoginType
        {
            get { return loginType; }
            set { SetProperty(ref loginType, value); }
        }
        private string username;
        public string Username
        {
            get { return username; }
            set { SetProperty(ref username, value); }
        }
        private string password;
        public string Password
        {
            get { return password; }
            set { SetProperty(ref password, value); }
        }
        private string confirmPassword;
        public string ConfirmPassword
        {
            get { return confirmPassword; }
            set { SetProperty(ref confirmPassword, value); }
        }
    }
    public class ComputerLoginType
    {
        public string DisplayName { get; set; }
        public int Type { get; set; }
    }
}

using Prism.Mvvm;

namespace Fortress.Mobile.Core.Models
{
    public class WebPasswordView : BindableBase
    {
        private bool hasOtpSecret;
        public bool HasOtpSecret
        {
            get { return hasOtpSecret; }
            set { SetProperty(ref hasOtpSecret, value); }
        }
        private string otpSecret;
        public string OtpSecret
        {
            get { return otpSecret; }
            set { SetProperty(ref otpSecret, value); }
        }
        private string confirmOtpSecret;
        public string ConfirmOtpSecret
        {
            get { return confirmOtpSecret; }
            set { SetProperty(ref confirmOtpSecret, value); }
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
}

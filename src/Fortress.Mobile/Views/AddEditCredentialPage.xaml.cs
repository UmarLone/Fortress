using Fortress.ViewModels;

namespace Fortress.Views
{
    public partial class AddEditCredentialPage : ContentPage
    {
        private AddEditCredentialPageViewModel Vm =>
            BindingContext as AddEditCredentialPageViewModel;

        public AddEditCredentialPage()
        {
            InitializeComponent();

            var fieldDomain     = this.FindByName<Controls.VaultFormField>("FieldDomain");
            var fieldUsername   = this.FindByName<Controls.VaultFormField>("FieldUsername");
            var fieldOtpSecret  = this.FindByName<Entry>("FieldOtpSecret");

            if (fieldDomain     != null) fieldDomain.TextChanged     += OnAnyFieldChanged;
            if (fieldUsername   != null) fieldUsername.TextChanged   += OnAnyFieldChanged;
            // Password Entry is a plain Entry (not VaultFormField) – subscribe directly
            if (FieldPassword != null) FieldPassword.TextChanged += OnAnyFieldChanged;
            if (fieldOtpSecret  != null) fieldOtpSecret.TextChanged   += OnAnyFieldChanged;
        }

        private void OnAnyFieldChanged(object sender, TextChangedEventArgs e)
            => Vm?.Validate();
    }
}

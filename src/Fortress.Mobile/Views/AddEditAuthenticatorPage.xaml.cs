using Fortress.ViewModels;

namespace Fortress.Views
{
    public partial class AddEditAuthenticatorPage : ContentPage
    {
        private AddEditAuthenticatorPageViewModel Vm =>
            BindingContext as AddEditAuthenticatorPageViewModel;

        public AddEditAuthenticatorPage()
        {
            InitializeComponent();

            var fieldIssuer  = this.FindByName<Controls.VaultFormField>("FieldIssuer");
            var fieldAccount = this.FindByName<Controls.VaultFormField>("FieldAccount");
            var fieldSecret  = this.FindByName<Controls.VaultFormField>("FieldSecret");
            var fieldPeriod  = this.FindByName<Controls.VaultFormField>("FieldPeriod");

            if (fieldIssuer  != null) fieldIssuer.TextChanged  += OnAnyFieldChanged;
            if (fieldAccount != null) fieldAccount.TextChanged += OnAnyFieldChanged;
            if (fieldSecret  != null) fieldSecret.TextChanged  += OnAnyFieldChanged;
            if (fieldPeriod  != null) fieldPeriod.TextChanged  += OnAnyFieldChanged;
        }

        private void OnAnyFieldChanged(object sender, TextChangedEventArgs e)
            => Vm?.Validate();
    }
}

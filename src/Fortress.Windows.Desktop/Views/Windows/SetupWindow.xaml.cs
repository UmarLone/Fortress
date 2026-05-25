using Fortress.Windows.Desktop.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;

namespace Fortress.Windows.Desktop.Views.Windows
{
    public partial class SetupWindow
    {
        public SetupViewModel ViewModel { get; }

        public SetupWindow(SetupViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;

            // ui:PasswordBox.Password is not a DependencyProperty — two-way
            // binding silently fails. Push values manually via PasswordChanged.
            PwBox.PasswordChanged += (_, _) => ViewModel.MasterPassword = PwBox.Password;
            ConfirmPwBox.PasswordChanged += (_, _) => ViewModel.MasterPasswordConfirm = ConfirmPwBox.Password;

            viewModel.OnSetupComplete += (_, _) =>
                 {
                     var main = (System.Windows.Window)App.Services.GetRequiredService<INavigationWindow>();
                     main.Show();

                     Close();
                 };
        }
    }
}

using Fortress.Windows.Desktop.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Fortress.Windows.Desktop.Views.Windows
{
    public partial class LockScreenWindow
    {
        public LockScreenViewModel ViewModel { get; }

        public LockScreenWindow(LockScreenViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;

            // ui:PasswordBox.Password is not a DependencyProperty — two-way
            // binding silently fails. Read the value in the PasswordChanged
            // event and push it into the ViewModel manually.
            PasswordInput.PasswordChanged += (_, _) =>
                ViewModel.Password = PasswordInput.Password;

            viewModel.OnUnlocked += (_, _) =>
            {
                var main = (System.Windows.Window)App.Services.GetRequiredService<INavigationWindow>();
                main.Show();
                Close();
            };
        } 
    }
}

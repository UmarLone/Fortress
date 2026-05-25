using Fortress.Core.Security;
using Fortress.Windows.Desktop.Views.Pages;
using Fortress.Windows.Desktop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Managed host of the application.
    /// </summary>
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public ApplicationHostService(IServiceProvider serviceProvider)
            => _serviceProvider = serviceProvider;

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
            => await HandleActivationAsync();

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Lock the vault on exit so the in-memory key is wiped
            _serviceProvider.GetService<IVaultSessionService>()?.Lock();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates main window during activation.
        /// </summary>
        private async Task HandleActivationAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var session = _serviceProvider.GetRequiredService<IVaultSessionService>();

                if (!session.IsSetupComplete)
                {
                    // First run — show the onboarding wizard
                    var onboarding = _serviceProvider.GetRequiredService<OnboardingWindow>();
                    onboarding.Show();
                    return;
                }

                // Returning user — show the lock screen
                // (MainWindow is shown by LockScreenWindow after successful unlock)
                var lockScreen = _serviceProvider.GetRequiredService<LockScreenWindow>();
                lockScreen.Show();
            });

            await Task.CompletedTask;
        }
    }
}

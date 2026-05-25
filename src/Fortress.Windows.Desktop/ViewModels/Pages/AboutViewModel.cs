using System.Reflection;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private string _appVersion = "v1.0.0";

        [ObservableProperty]
        private string _buildDate = string.Empty;

   public Task OnNavigatedToAsync()
     {
            var asm = Assembly.GetExecutingAssembly();
   var ver = asm.GetName().Version;
 AppVersion = ver is not null
            ? $"v{ver.Major}.{ver.Minor}.{ver.Build}"
  : "v1.0.0";

      try
    {
           BuildDate = System.IO.File
             .GetLastWriteTime(asm.Location)
   .ToString("MMMM d, yyyy");
            }
       catch
 {
   BuildDate = DateTime.Today.ToString("MMMM d, yyyy");
     }

     return Task.CompletedTask;
}

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

   [RelayCommand]
        private void CheckForUpdates()
        {
   try
       {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
   "https://github.com/UmarLone/Fortress/releases")
         { UseShellExecute = true });
         }
       catch { }
        }

        [RelayCommand]
private void ViewLicenses()
  {
       try
     {
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
           "https://github.com/UmarLone/Fortress/blob/master/LICENSE")
        { UseShellExecute = true });
            }
    catch { }
        }
    }
}

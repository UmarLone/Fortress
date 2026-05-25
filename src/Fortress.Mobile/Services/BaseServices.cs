using Prism.Common;
using The49.Maui.BottomSheet;

namespace Fortress.Mobile.Services
{
    [Scoped]
    public record BaseServices(
        IConfiguration Configuration,
        INavigationService Navigator,
        IDialogService Dialogs,
        ILoggerFactory LoggerFactory
    );
   
}
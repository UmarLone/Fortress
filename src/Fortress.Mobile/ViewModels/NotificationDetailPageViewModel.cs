using Fortress.Mobile.Core.Models;

namespace Fortress.ViewModels;

public class NotificationDetailPageViewModel : ViewModelBase
{
    #region Properties

    private UserNotification _notification;
    public UserNotification Notification
    {
        get => _notification;
        set
        {
            SetProperty(ref _notification, value);
            RaisePropertyChanged(nameof(TypeLabel));
            RaisePropertyChanged(nameof(TypeBadgeColor));
            RaisePropertyChanged(nameof(TypeTextColor));
            RaisePropertyChanged(nameof(TypeIconGlyph));
            RaisePropertyChanged(nameof(TypeIconColor));
            RaisePropertyChanged(nameof(FormattedDate));
        }
    }

    public string FormattedDate =>
        Notification is null
    ? string.Empty
    : Notification.CreationDateTime.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt");

    public string TypeLabel => Notification?.Type switch
    {
        NotificationType.Success => "SUCCESS",
        NotificationType.Warning => "WARNING",
        NotificationType.Error => "ERROR",
        NotificationType.Critical => "CRITICAL",
        NotificationType.Ask => "ACTION",
        _ => "INFO",
    };

    public Color TypeBadgeColor => Notification?.Type switch
    {
        NotificationType.Success => Color.FromArgb("#DCFCE7"),
        NotificationType.Warning => Color.FromArgb("#FEF3C7"),
        NotificationType.Error => Color.FromArgb("#FEE2E2"),
        NotificationType.Critical => Color.FromArgb("#FEE2E2"),
        _ => Color.FromArgb("#EFF6FF"),
    };

    public Color TypeTextColor => Notification?.Type switch
    {
        NotificationType.Success => Color.FromArgb("#16A34A"),
        NotificationType.Warning => Color.FromArgb("#D97706"),
        NotificationType.Error => Color.FromArgb("#DC2626"),
        NotificationType.Critical => Color.FromArgb("#DC2626"),
        _ => Color.FromArgb("#407cca"),
    };

    // Material icon glyphs matching MaterialIcons.cs constants
    public string TypeIconGlyph => Notification?.Type switch
    {
        NotificationType.Success => "\ue86c", // check_circle
        NotificationType.Warning => "\ue002", // warning
        NotificationType.Error => "\ue000", // error
        NotificationType.Critical => "\uf012", // gpp_bad
        NotificationType.Ask => "\ue8fd", // help_outline
        _ => "\ue88e", // info
    };

    public Color TypeIconColor => TypeTextColor;

    #endregion

    #region Commands

    private DelegateCommand? _goBackCommand;
    public DelegateCommand GoBackCommand =>
        _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

    #endregion

    public NotificationDetailPageViewModel(INavigationService navigationService)
        : base(navigationService) { }

    public override void OnNavigatedTo(INavigationParameters parameters)
    {
        base.OnNavigatedTo(parameters);
        if (parameters.ContainsKey("Notification"))
            Notification = parameters.GetValue<UserNotification>("Notification");
    }
}

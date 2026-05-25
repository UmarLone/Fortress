using Fortress.Mobile.Adapters;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;

namespace Fortress.Services
{
    /// <summary>
    /// Convenience extension methods on <see cref="IBottomSheetService"/> that
    /// replicate the IUserDialogs API (Alert / Confirm / Prompt / Destructive)
    /// using the fully-native <see cref="AlertSheet"/> bottom sheet.
    /// </summary>
    public static class AlertSheetExtensions
    {
        /// <summary>Shows an informational alert with a single OK button.</summary>
        public static Task<AlertSheetResult> AlertAsync(
             this IBottomSheetService svc,
            string title, string message, string okText = "OK") =>
                   svc.ShowAsync<AlertSheet, AlertSheetViewModel, AlertSheetResult>(
        AlertSheetConfig.Alert(title, message, okText));

        /// <summary>Asks a yes/no question. Returns true when Confirm is tapped.</summary>
        public static async Task<bool> ConfirmAsync(
       this IBottomSheetService svc,
        string title, string message,
      string confirmText = "Confirm", string cancelText = "Cancel")
        {
            var r = await svc.ShowAsync<AlertSheet, AlertSheetViewModel, AlertSheetResult>(
            AlertSheetConfig.Confirm(title, message, confirmText, cancelText));
            return r?.Confirmed == true;
        }

        /// <summary>
        /// Shows a destructive-action sheet (red Delete button).
        /// Returns true when the destructive button is tapped.
        /// </summary>
        public static async Task<bool> DestructiveConfirmAsync(
              this IBottomSheetService svc,
              string title, string message,
              string destructiveText = "Delete", string cancelText = "Cancel")
        {
            var r = await svc.ShowAsync<AlertSheet, AlertSheetViewModel, AlertSheetResult>(
                 AlertSheetConfig.Destructive(title, message, destructiveText, cancelText));
            return r?.Destructive == true;
        }

        /// <summary>
        /// Shows a text-input prompt.
        /// Returns the entered text when Confirm is tapped, null when cancelled.
        /// </summary>
        public static async Task<string?> PromptAsync(
                  this IBottomSheetService svc,
                  string title, string message,
                  string placeholder = "Type here…",
                  string? defaultText = null,
                  string confirmText = "Save",
       string cancelText = "Cancel")
        {
            var r = await svc.ShowAsync<AlertSheet, AlertSheetViewModel, AlertSheetResult>(
                  AlertSheetConfig.Prompt(title, message, placeholder, defaultText, confirmText, cancelText));
            return r?.Confirmed == true ? r.InputText : null;
        }
    }
}

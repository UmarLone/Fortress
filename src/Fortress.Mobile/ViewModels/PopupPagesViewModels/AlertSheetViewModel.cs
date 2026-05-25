using Fortress.Mobile.Adapters;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    // ── Result ───────────────────────────────────────────────────────────────────
    public sealed class AlertSheetResult
    {
        public bool Confirmed { get; init; }
        public bool Destructive { get; init; }
        public string? InputText { get; init; }

        public static AlertSheetResult Cancel() => new();
        public static AlertSheetResult Ok() => new() { Confirmed = true };
        public static AlertSheetResult Delete() => new() { Destructive = true };
        public static AlertSheetResult Input(string text) => new() { Confirmed = true, InputText = text };
    }

    // ── Config ───────────────────────────────────────────────────────────────────
    public sealed class AlertSheetConfig
    {
        public MauiIcon? Icon { get; private set; }
        public Color IconColor { get; private set; } = Color.FromArgb("#407cca");
        public Color IconTileColor { get; private set; } = Color.FromArgb("#DBEAFE");
        public string? Title { get; private set; }
        public string? Message { get; private set; }
        public string? ConfirmText { get; private set; }
        public Color ConfirmBg { get; private set; } = Color.FromArgb("#407cca");
        public Color ConfirmFg { get; private set; } = Colors.White;
        public string? DestructiveText { get; private set; }
        public string? CancelText { get; private set; }
        public bool ShowInput { get; private set; }
        public string? InputPlaceholder { get; private set; }
        public string? InputDefault { get; private set; }

        // ── Fluent setters ─────────────────────────────────────────────────────
        public AlertSheetConfig WithTitle(string title)
        { Title = title; return this; }

        public AlertSheetConfig WithMessage(string message)
        { Message = message; return this; }

        public AlertSheetConfig WithIcon(MauiIcon icon, Color iconColor = null, Color tileColor = null)
        {
            Icon = icon;
            if (iconColor != null) IconColor = iconColor;
            if (tileColor != null) IconTileColor = tileColor;
            return this;
        }

        public AlertSheetConfig WithConfirm(string text, Color bg = null, Color fg = null)
        {
            ConfirmText = text;
            if (bg != null) ConfirmBg = bg;
            if (fg != null) ConfirmFg = fg;
            return this;
        }

        public AlertSheetConfig WithDestructive(string text)
        { DestructiveText = text; return this; }

        public AlertSheetConfig WithCancel(string text = "Cancel")
        { CancelText = text; return this; }

        public AlertSheetConfig WithInput(string placeholder = "Type here\u2026", string? defaultText = null)
        {
            ShowInput = true;
            InputPlaceholder = placeholder;
            InputDefault = defaultText;
            return this;
        }

        // ── Convenience factories ──────────────────────────────────────────────
        public static AlertSheetConfig Alert(string title, string message, string okText = "OK") =>
   new AlertSheetConfig()
       .WithTitle(title)
          .WithMessage(message)
          .WithConfirm(okText);

        public static AlertSheetConfig Confirm(string title, string message,
            string confirmText = "Confirm", string cancelText = "Cancel") =>
       new AlertSheetConfig()
                .WithTitle(title)
  .WithMessage(message)
         .WithConfirm(confirmText)
     .WithCancel(cancelText);

        public static AlertSheetConfig Destructive(string title, string message,
      string destructiveText = "Delete", string cancelText = "Cancel") =>
 new AlertSheetConfig()
              .WithIcon(
  new MauiIcon().Icon(MaterialIcons.Delete),
          Color.FromArgb("#DC2626"),
          Color.FromArgb("#FEE2E2"))
.WithTitle(title)
  .WithMessage(message)
     .WithDestructive(destructiveText)
  .WithCancel(cancelText);

        public static AlertSheetConfig Prompt(string title, string message,
              string placeholder = "Type here\u2026", string? defaultText = null,
               string confirmText = "Save", string cancelText = "Cancel") =>
          new AlertSheetConfig()
                     .WithTitle(title)
         .WithMessage(message)
          .WithInput(placeholder, defaultText)
         .WithConfirm(confirmText)
               .WithCancel(cancelText);
    }

    // ── ViewModel ────────────────────────────────────────────────────────────────
    public sealed class AlertSheetViewModel : BottomSheetViewModelBase
    {
        // ── Bound properties ───────────────────────────────────────────────────
        private string? _title;
        public string? Title { get => _title; set => SetProperty(ref _title, value); }

        private string? _message;
        public string? Message { get => _message; set => SetProperty(ref _message, value); }

        private MauiIcon? _sheetIcon;
        public MauiIcon? SheetIcon { get => _sheetIcon; set => SetProperty(ref _sheetIcon, value); }

        private Color _iconColor = Color.FromArgb("#407cca");
        public Color IconColor { get => _iconColor; set => SetProperty(ref _iconColor, value); }

        private Color _iconTileColor = Color.FromArgb("#DBEAFE");
        public Color IconTileColor { get => _iconTileColor; set => SetProperty(ref _iconTileColor, value); }

        private string? _confirmText;
        public string? ConfirmText { get => _confirmText; set => SetProperty(ref _confirmText, value); }

        private Brush _confirmButtonBackground = new SolidColorBrush(Color.FromArgb("#407cca"));
        public Brush ConfirmButtonBackground
        {
            get => _confirmButtonBackground;
            set => SetProperty(ref _confirmButtonBackground, value);
        }

        private Color _confirmTextColor = Colors.White;
        public Color ConfirmTextColor { get => _confirmTextColor; set => SetProperty(ref _confirmTextColor, value); }

        private string? _destructiveText;
        public string? DestructiveText { get => _destructiveText; set => SetProperty(ref _destructiveText, value); }

        private string? _cancelText;
        public string? CancelText { get => _cancelText; set => SetProperty(ref _cancelText, value); }

        private bool _showInput;
        public bool ShowInput { get => _showInput; set => SetProperty(ref _showInput, value); }

        private string? _inputPlaceholder;
        public string? InputPlaceholder { get => _inputPlaceholder; set => SetProperty(ref _inputPlaceholder, value); }

        private string? _inputText;
        public string? InputText { get => _inputText; set => SetProperty(ref _inputText, value); }

        // ── Computed ───────────────────────────────────────────────────────────
        public bool HasTitle => !string.IsNullOrEmpty(Title);
        public bool HasMessage => !string.IsNullOrEmpty(Message);
        public bool HasIcon => SheetIcon is not null;
        public bool HasConfirmButton => !string.IsNullOrEmpty(ConfirmText);
        public bool HasDestructiveButton => !string.IsNullOrEmpty(DestructiveText);
        public bool HasCancelButton => !string.IsNullOrEmpty(CancelText);

        // ── Initialise from config ─────────────────────────────────────────────
        public override Task InitializeAsync(object args, string title = null)
        {
            if (args is not AlertSheetConfig cfg)
                return Task.CompletedTask;

            Title = cfg.Title;
            Message = cfg.Message;
            SheetIcon = cfg.Icon;
            IconColor = cfg.IconColor;
            IconTileColor = cfg.IconTileColor;
            ConfirmText = cfg.ConfirmText;
            ConfirmButtonBackground = new SolidColorBrush(cfg.ConfirmBg);
            ConfirmTextColor = cfg.ConfirmFg;
            DestructiveText = cfg.DestructiveText;
            CancelText = cfg.CancelText;
            ShowInput = cfg.ShowInput;
            InputPlaceholder = cfg.InputPlaceholder;
            InputText = cfg.InputDefault;

            RaisePropertyChanged(nameof(HasTitle));
            RaisePropertyChanged(nameof(HasMessage));
            RaisePropertyChanged(nameof(HasIcon));
            RaisePropertyChanged(nameof(HasConfirmButton));
            RaisePropertyChanged(nameof(HasDestructiveButton));
            RaisePropertyChanged(nameof(HasCancelButton));

            return Task.CompletedTask;
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private DelegateCommand? _confirmCommand;
        public DelegateCommand ConfirmCommand => _confirmCommand ??= new DelegateCommand(() =>
        {
            var result = ShowInput
             ? AlertSheetResult.Input(InputText ?? string.Empty)
             : AlertSheetResult.Ok();
            ReturnResult?.Invoke(result);
            DismissAction?.Invoke();
        });

        private DelegateCommand? _destructiveCommand;
        public DelegateCommand DestructiveCommand => _destructiveCommand ??= new DelegateCommand(() =>
 {
     ReturnResult?.Invoke(AlertSheetResult.Delete());
     DismissAction?.Invoke();
 });

        private DelegateCommand? _cancelCommand;
        public DelegateCommand CancelCommand => _cancelCommand ??= new DelegateCommand(() =>
        {
            ReturnResult?.Invoke(AlertSheetResult.Cancel());
            DismissAction?.Invoke();
        });
    }
}

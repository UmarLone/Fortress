using System.Windows.Input;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultGeneratedPasswordCard : ContentView
    {
        // ── Password ──────────────────────────────────────────────────────────
        public static readonly BindableProperty PasswordProperty =
BindableProperty.Create(nameof(Password), typeof(string), typeof(VaultGeneratedPasswordCard), string.Empty);

        public string Password
        {
      get => (string)GetValue(PasswordProperty);
  set => SetValue(PasswordProperty, value);
        }

 // ── RefreshCommand ────────────────────────────────────────────────────
    public static readonly BindableProperty RefreshCommandProperty =
            BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(VaultGeneratedPasswordCard), null);

 public ICommand RefreshCommand
   {
            get => (ICommand)GetValue(RefreshCommandProperty);
 set => SetValue(RefreshCommandProperty, value);
   }

     // ── CopyCommand ───────────────────────────────────────────────────────
  public static readonly BindableProperty CopyCommandProperty =
      BindableProperty.Create(nameof(CopyCommand), typeof(ICommand), typeof(VaultGeneratedPasswordCard), null);

        public ICommand CopyCommand
    {
            get => (ICommand)GetValue(CopyCommandProperty);
            set => SetValue(CopyCommandProperty, value);
  }

 public VaultGeneratedPasswordCard()
        {
            InitializeComponent();
        }
    }
}

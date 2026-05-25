namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class VaultPasswordStrengthBar : ContentView
    {
    // ── Score ──────────────────────────────────────────────────────────────
        public static readonly BindableProperty ScoreProperty =
            BindableProperty.Create(nameof(Score), typeof(int), typeof(VaultPasswordStrengthBar), 0);

        public int Score
   {
      get => (int)GetValue(ScoreProperty);
       set => SetValue(ScoreProperty, value);
 }

        public VaultPasswordStrengthBar()
        {
  InitializeComponent();
        }
    }
}

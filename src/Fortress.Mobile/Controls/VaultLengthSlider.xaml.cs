using System.Windows.Input;

namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class VaultLengthSlider : ContentView
    {
        // ── Length ────────────────────────────────────────────────────────────
        public static readonly BindableProperty LengthProperty =
   BindableProperty.Create(nameof(Length), typeof(int), typeof(VaultLengthSlider), 15, BindingMode.TwoWay);

public int Length
        {
    get => (int)GetValue(LengthProperty);
   set => SetValue(LengthProperty, value);
        }

    // ── Minimum ───────────────────────────────────────────────────────────
public static readonly BindableProperty MinimumProperty =
   BindableProperty.Create(nameof(Minimum), typeof(double), typeof(VaultLengthSlider), 6.0);

public double Minimum
      {
   get => (double)GetValue(MinimumProperty);
      set => SetValue(MinimumProperty, value);
     }

  // ── Maximum ───────────────────────────────────────────────────────────
        public static readonly BindableProperty MaximumProperty =
   BindableProperty.Create(nameof(Maximum), typeof(double), typeof(VaultLengthSlider), 64.0);

     public double Maximum
      {
   get => (double)GetValue(MaximumProperty);
      set => SetValue(MaximumProperty, value);
        }

      // ── IncreaseCommand ───────────────────────────────────────────────────
        public static readonly BindableProperty IncreaseCommandProperty =
   BindableProperty.Create(nameof(IncreaseCommand), typeof(ICommand), typeof(VaultLengthSlider), null);

      public ICommand IncreaseCommand
        {
            get => (ICommand)GetValue(IncreaseCommandProperty);
 set => SetValue(IncreaseCommandProperty, value);
        }

        // ── DecreaseCommand ───────────────────────────────────────────────────
 public static readonly BindableProperty DecreaseCommandProperty =
   BindableProperty.Create(nameof(DecreaseCommand), typeof(ICommand), typeof(VaultLengthSlider), null);

        public ICommand DecreaseCommand
        {
        get => (ICommand)GetValue(DecreaseCommandProperty);
       set => SetValue(DecreaseCommandProperty, value);
  }

        public VaultLengthSlider()
        {
     InitializeComponent();
        }
    }
}

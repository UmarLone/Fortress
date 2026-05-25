using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Fortress.Controls
{
    public class VaultFilterChip : ContentView
    {
        // ── Bindable properties ────────────────────────────────────────────
        public static readonly BindableProperty LabelProperty =
            BindableProperty.Create(nameof(Label), typeof(string), typeof(VaultFilterChip), string.Empty,
                propertyChanged: (b, _, n) => ((VaultFilterChip)b)._label.Text = (string)n);
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly BindableProperty IsActiveProperty =
            BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(VaultFilterChip), false,
                propertyChanged: (b, _, n) => ((VaultFilterChip)b).ApplyActiveState((bool)n));
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(VaultFilterChip), null);
        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(VaultFilterChip), null);
        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        // ── Private view references ────────────────────────────────────────
        private readonly Border _border;
        private readonly Label _label;
        private readonly GradientStop _stop1;
        private readonly GradientStop _stop2;

        // ── Constructor ────────────────────────────────────────────────────
        public VaultFilterChip()
        {
            _stop1 = new GradientStop { Offset = 0f, Color = Colors.Transparent };
            _stop2 = new GradientStop { Offset = 1f, Color = Colors.Transparent };

            _label = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                InputTransparent = true,
                VerticalOptions = LayoutOptions.Center,
            };

            _border = new Border
            {
                Padding = new Thickness(16, 9),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
                Background = new LinearGradientBrush(
                    new GradientStopCollection { _stop1, _stop2 },
                    new Point(0, 0), new Point(1, 1)),
                Content = _label,
            };

            _border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    var cmd = (ICommand)GetValue(CommandProperty);
                    var param = GetValue(CommandParameterProperty);
                    if (cmd?.CanExecute(param) == true)
                        cmd.Execute(param);
                })
            });

            Content = _border;

            // Apply initial inactive styling
            ApplyActiveState(false);
        }

        // ── State ──────────────────────────────────────────────────────────
        private void ApplyActiveState(bool isActive)
        {
            if (Application.Current?.Resources is null) return;
            var res = Application.Current.Resources;

            if (isActive)
            {
                _stop1.Color = Colors.Transparent;
                _stop2.Color = Colors.Transparent;
                _border.BackgroundColor = (Color)res["PrimaryColor"];
                _label.TextColor = Colors.White;
            }
            else
            {
                _stop1.Color = Colors.Transparent;
                _stop2.Color = Colors.Transparent;
                _border.BackgroundColor = (Color)res["Gray-100"];
                _label.TextColor = (Color)res["TextSecondaryColor"];
            }
        }
    }
}

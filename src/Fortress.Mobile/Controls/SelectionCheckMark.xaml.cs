namespace Fortress.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SelectionCheckMark : ContentView
    {
        public static readonly BindableProperty IsCheckedProperty =
            BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(SelectionCheckMark),
                false, BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public SelectionCheckMark()
        {
            InitializeComponent();
        }

        private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SelectionCheckMark control && newValue is bool isChecked)
            {
                var unchecked_ = control.FindByName<Border>("UncheckedBorder");
                var checked_ = control.FindByName<Border>("CheckedBorder");
                if (unchecked_ != null) unchecked_.IsVisible = !isChecked;
                if (checked_ != null) checked_.IsVisible = isChecked;
            }
        }
    }
}

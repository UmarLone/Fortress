 
namespace Fortress.Behaviors
{
    public class FocusBehavior : Behavior<Entry>
    {
        static readonly BindablePropertyKey IsFocusedPropertyKey = BindableProperty.CreateReadOnly("IsFocused", typeof(bool), typeof(FocusBehavior), default(bool));
        public static readonly BindableProperty IsFocusedProperty = IsFocusedPropertyKey.BindableProperty;

        public bool IsFocused
        {
            get { return (bool)GetValue(IsFocusedProperty); }
            private set { SetValue(IsFocusedPropertyKey, value); }
        }

        protected override void OnAttachedTo(Entry bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.Focused += OnEntryFocused;
            bindable.Unfocused += OnEntryUnfocused;
        }

        protected override void OnDetachingFrom(Entry bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.Focused -= OnEntryFocused;
            bindable.Unfocused -= OnEntryUnfocused;
        }

        void OnEntryFocused(object sender, EventArgs args)
        {
            IsFocused = true;
        }

        void OnEntryUnfocused(object sender, EventArgs args)
        {
            IsFocused = false;
        }
    }

}

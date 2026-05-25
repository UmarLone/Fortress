
namespace Fortress.Mobile.Adapters
{
    public abstract class BottomSheetViewModelBase : BindableBase
    {
        public Action DismissAction { get; set; }

        public Action<object> ReturnResult { get; set; }

        public virtual Task InitializeAsync(object args, string title = null) => Task.CompletedTask;
    }
}

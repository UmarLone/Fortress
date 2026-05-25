namespace Fortress.Mobile.Adapters
{
    public abstract class BaseBottomSheet<TViewModel>
      : The49.Maui.BottomSheet.BottomSheet
      where TViewModel : BottomSheetViewModelBase
    {
        protected TViewModel ViewModel => (TViewModel)BindingContext;
        protected BaseBottomSheet()
        {
             
            this.Dismissed += async (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.DismissAction?.Invoke();
                }
                BottomSheetManager.Clear();
            };
        }
    }
}

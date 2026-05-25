namespace Fortress.Mobile.Adapters
{
    public interface IBottomSheetService
    {
        Task<T> ShowAsync<TSheet, TViewModel, T>(object args = null,string title= null)
            where TSheet : The49.Maui.BottomSheet.BottomSheet
            where TViewModel : BottomSheetViewModelBase;
    }
}

using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.BottomSheet;
using Button = Android.Widget.Button;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;
namespace Fortress.Droid.Renderers
{
    public class ConfirmBottomSheetFragment : BottomSheetDialogFragment
    {
        private readonly string _description;

        public event Action OnConfirmed;
        public event Action OnCancelled;

        public ConfirmBottomSheetFragment(
            string description)
        {
            _description = description;
        }
        public override Dialog OnCreateDialog(Bundle? savedInstanceState)
        {
            return new BottomSheetDialog(
                Context!,
                Resource.Style.BottomSheetDialogTheme);
        }
        public override View OnCreateView(
            LayoutInflater inflater,
            ViewGroup container,
            Bundle savedInstanceState)
        {
            var view = inflater.Inflate(
                Resource.Layout.bs_confirm,
                container,
                false);

            var descriptionText = view.FindViewById<TextView>(Resource.Id.descriptionText);
            var cancelButton = view.FindViewById<Button>(Resource.Id.cancelButton);
            var okButton = view.FindViewById<Button>(Resource.Id.okButton);

            descriptionText.Text = _description;

            cancelButton.Click += (_, __) =>
            {
                OnCancelled?.Invoke();
                Dismiss();
            };

            okButton.Click += (_, __) =>
            {
                OnConfirmed?.Invoke();
                Dismiss();
            };

            return view;
        }
    }

}
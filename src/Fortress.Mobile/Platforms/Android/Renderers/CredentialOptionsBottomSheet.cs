using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.BottomSheet;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;
namespace Fortress.Droid.Renderers
{
    public class CredentialOptionsBottomSheet : BottomSheetDialogFragment
    {
        private readonly IList<BottomSheetOption> _options;

        public CredentialOptionsBottomSheet(IList<BottomSheetOption> options)
        {
            _options = options;
        }
        
        public override Dialog OnCreateDialog(Bundle? savedInstanceState)
        {
            return new BottomSheetDialog(
                Context!,
                Resource.Style.BottomSheetDialogTheme);
        }
        public override void OnStart()
        {
            base.OnStart();

            if (Dialog is not BottomSheetDialog dialog)
                return;

            var bottomSheet = dialog.FindViewById<FrameLayout>(
                Resource.Id.design_bottom_sheet);

            if (bottomSheet == null)
                return;

            //// Lock height to 60% of screen (or any value you want)
            //var displayMetrics = Resources.DisplayMetrics;
            //var fixedHeight = (int)(displayMetrics.HeightPixels * 0.3);

            //bottomSheet.LayoutParameters.Height = fixedHeight;
            //bottomSheet.RequestLayout();
            //var behavior = BottomSheetBehavior.From(bottomSheet);
            //behavior.State = BottomSheetBehavior.StateExpanded;
            //behavior.SkipCollapsed = true;
            //behavior.Draggable = true;
        }
        public override View OnCreateView(
            LayoutInflater inflater,
            ViewGroup container,
            Bundle savedInstanceState)
        {
            var view = inflater.Inflate(
                Resource.Layout.bs_options_list,
                container,
                false);

            var recycler = view.FindViewById<RecyclerView>(Resource.Id.optionsList);
            recycler.SetLayoutManager(new LinearLayoutManager(Context));
            recycler.SetAdapter(new OptionsAdapter(_options, Dismiss));

            return view;
        }
    }

}
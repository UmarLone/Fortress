//using Android.Views;
//using AndroidX.AppCompat.Widget;
//using Android.Content;
//using Fortress.Views;
//using Fortress.Droid;
//using Microsoft.Maui.Controls.Compatibility;
//using Microsoft.Maui.Controls.Compatibility.Platform.Android;
//using Microsoft.Maui.Controls.Platform;
//using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

//[assembly: ExportRenderer(typeof(AutofillPage), typeof(ToolbarButtonRenderer))]

//namespace Fortress.Droid
//{
//    public class ToolbarButtonRenderer : PageRenderer
//    {
//        public ToolbarButtonRenderer(Context context) : base(context)
//        {

//        }
//        protected override void OnElementChanged(ElementChangedEventArgs<Page> e)
//        {
//            base.OnElementChanged(e);

//            if (e.NewElement != null && e.NewElement is AutofillPage page)
//            {
//                AddCustomButtonToToolbar();
//            }
//        }

//        void AddCustomButtonToToolbar()
//        {
//            if (GetToolbar() is Toolbar toolBar)
//            {

//                // Add your custom button
//                if (toolBar.Menu?.FindItem(Resource.Id.OpenFortressAction) == null)
//                {
//                    var customItem = toolBar.Menu?.Add(0, Resource.Id.OpenFortressAction, 0, "Custom");
//                    customItem?.SetIcon(Resource.Drawable.open);
//                    customItem?.SetShowAsAction(ShowAsAction.Always);

//                    // Handle the click event for your custom button
//                    customItem?.SetOnMenuItemClickListener(new MenuItemClickListener());
//                }
//            }
//        }

//        Toolbar? GetToolbar()
//        {
//            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
//            if (activity?.Window?.DecorView.RootView is ViewGroup viewGroup)
//            {
//                var toolbars = GetToolbars(viewGroup);

//                //Return top-most Toolbar
//                return toolbars.LastOrDefault();
//            }

//            return null;
//        }
//        static IEnumerable<Toolbar> GetToolbars(ViewGroup viewGroup)
//        {
//            for (int i = 0; i < viewGroup.ChildCount; i++)
//            {
//                if (viewGroup.GetChildAt(i) is Toolbar toolbar)
//                {
//                    yield return toolbar;
//                }
//                else if (viewGroup.GetChildAt(i) is ViewGroup childViewGroup)
//                {
//                    foreach (var childToolbar in GetToolbars(childViewGroup))
//                        yield return childToolbar;
//                }
//            }
//        }
//    }
//}





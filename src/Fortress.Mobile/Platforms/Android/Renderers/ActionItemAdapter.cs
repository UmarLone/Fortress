using Android.Content;
using Android.Views;
using Android.Widget;
using Resource = Microsoft.Maui.Resource;
namespace Fortress.Droid.Renderers
{

    public class ActionItem
    {
        public string Title { get; set; }
        public int Icon { get; set; }
        public Action Action { get; set; }
    }
    public class ActionItemAdapter : BaseAdapter<ActionItem>
    {
        private List<ActionItem> items;
        private Context context;

        public ActionItemAdapter(Context context, List<ActionItem> items)
        {
            this.context = context;
            this.items = items;
        }

        public override ActionItem this[int position] => items[position];

        public override int Count => items.Count;

        public override long GetItemId(int position) => position;

        public override Android.Views.View? GetView(int position, Android.Views.View? convertView, ViewGroup? parent)
        {
            var view = convertView ?? LayoutInflater.From(context).Inflate(Resource.Layout.bottomsheetlayout, parent, false);
            var titleTextView = view.FindViewById<TextView>(Resource.Id.actionTitle);
            var iconImageView = view.FindViewById<ImageView>(Resource.Id.actionIcon);

            var item = items[position];
            titleTextView.Text = item.Title;
            iconImageView.SetImageResource(item.Icon);

            view.Click += (sender, e) => item.Action?.Invoke();

            return view;
        }
    }

}
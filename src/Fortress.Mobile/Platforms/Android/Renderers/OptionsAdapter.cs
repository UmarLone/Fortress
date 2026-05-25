using Android.Content;
using Android.Text.Method;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.TextField;
using Newtonsoft.Json;
using ImageButton = Android.Widget.ImageButton;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;
namespace Fortress.Droid.Renderers
{
    public class OptionsAdapter : RecyclerView.Adapter
    {
        private readonly IList<BottomSheetOption> _items;
        private readonly Action _dismiss;

        public OptionsAdapter(
            IList<BottomSheetOption> items,
            Action dismiss)
        {
            _items = items;
            _dismiss = dismiss;
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(
            ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                .Inflate(Resource.Layout.row_option, parent, false);

            return new OptionViewHolder(view);
        }

        public override void OnBindViewHolder(
            RecyclerView.ViewHolder holder, int position)
        {
            var vh = (OptionViewHolder)holder;
            var option = _items[position];

            vh.Bind(option, async () =>
            {
                _dismiss();
                await option.Action();
            });
        }

        class OptionViewHolder : RecyclerView.ViewHolder
        {
            private readonly ImageView _icon;
            private readonly TextView _title;

            public OptionViewHolder(View itemView) : base(itemView)
            {
                _icon = itemView.FindViewById<ImageView>(Resource.Id.icon);
                _title = itemView.FindViewById<TextView>(Resource.Id.title);
            }

            public void Bind(BottomSheetOption option, Func<Task> onClick)
            {
                _icon.SetImageResource(option.IconResId);
                _title.Text = option.Title;

                ItemView.Click += async (_, __) => await onClick();
            }
        }
    }

}
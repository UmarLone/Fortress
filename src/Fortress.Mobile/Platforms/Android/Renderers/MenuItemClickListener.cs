using Android.App;
using Android.Content;
using Android.Views;
using Fortress.Mobile.Core.EventAggregators;

namespace Fortress.Droid
{
    public class MenuItemClickListener : Java.Lang.Object, IMenuItemOnMenuItemClickListener
    {
        public bool OnMenuItemClick(IMenuItem item)
        {
            Shiny.Hosting.Host.GetService<IEventAggregator>().GetEvent<StartMainAppEvent>().Publish(true);

            return true;
        }
        
      
    }
}





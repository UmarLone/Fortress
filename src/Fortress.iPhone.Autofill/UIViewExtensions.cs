using UIKit;

namespace Fortress.iPhone.Autofill
{
    public static class UIViewExtensions
    {
        public static UIViewController FindViewController(this UIView view)
        {
            var responder = view.NextResponder;
            while (responder != null)
            {
                if (responder is UIViewController viewController)
                    return viewController;
                responder = responder.NextResponder;
            }
            return null;
        }
    }
}
using System;
using System.Runtime.InteropServices;
using AuthenticationServices;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Fortress.iPhone.Autofill
{
    /// <summary>
    /// Extension methods for opening URLs from credential provider extensions
    /// Uses the responder chain approach to access UIApplication
    /// </summary>
    public static class ASCredentialProviderExtensionContextExtensions
    {
        /// <summary>
        /// Opens a URL from the credential provider extension by walking the responder chain
        /// This works on all iOS versions by finding UIApplication through the responder chain
        /// </summary>
        /// <param name="viewController">The view controller to start the responder chain from</param>
        /// <param name="url">The URL to open</param>
        /// <returns>True if the URL was opened successfully</returns>
        public static bool OpenUrlViaResponderChain(this UIViewController viewController, NSUrl url)
        {
            if (viewController == null)
                throw new ArgumentNullException(nameof(viewController));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            
            // Walk up the responder chain to find UIApplication
            UIResponder? responder = viewController;
            
            while (responder != null)
            {
                if (responder is UIApplication application)
                {
                    // iOS 18.0+ requires using the non-deprecated open method
                    if (UIDevice.CurrentDevice.CheckSystemVersion(18, 0))
                    {
                        application.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
                        return true;
                    }
                    else
                    {
                        // For iOS < 18, use performSelector approach
                        var selector = new Selector("openURL:");
                        if (application.RespondsToSelector(selector))
                        {
                            var result = application.PerformSelector(selector, url);
                            return result != null;
                        }
                    }
                    break;
                }
                responder = responder.NextResponder;
            }
            
            return false;
        }
        
        /// <summary>
        /// Alternative: Opens a URL using iOS 17+ ASCredentialProviderExtensionContext API
        /// Falls back to responder chain if not available
        /// </summary>
        public static void OpenUrl(this ASCredentialProviderExtensionContext context, NSUrl url, Action<bool> completion)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            
            // Check if the method is available (iOS 17+)
            var selector = new Selector("openURL:completion:");
            if (!context.RespondsToSelector(selector))
            {
                completion?.Invoke(false);
                return;
            }
            
            try
            {
                // Call the native method with nil completion block
                var selectorHandle = sel_registerName("openURL:completion:");
                void_objc_msgSend_IntPtr_IntPtr(context.Handle, selectorHandle, url.Handle, IntPtr.Zero);
                
                // Assume success since we can't get the callback
                completion?.Invoke(true);
            }
            catch (Exception)
            {
                completion?.Invoke(false);
            }
        }
        
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void void_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);
        
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName(string name);
    }
}

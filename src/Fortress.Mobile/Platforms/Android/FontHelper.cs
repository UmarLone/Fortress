using Android.Content;
using Android.Graphics;

namespace Fortress.Mobile.Platforms.Android
{
    /// <summary>
  /// Loads custom fonts from the Android Assets folder for use in
    /// native Android views (autofill bottom sheets, RemoteViews, etc.)
    /// where the MAUI resource pipeline is not available.
    /// </summary>
    internal static class FontHelper
    {
        private static Typeface? _audiowide;

        /// <summary>
        /// Returns the Audiowide-Regular typeface, loading it from assets
        /// on first call and caching it for all subsequent calls.
        /// </summary>
        public static Typeface Audiowide(Context context)
        {
    return _audiowide ??= Typeface.CreateFromAsset(
   context.Assets!, "Audiowide-Regular.ttf");
        }
    }
}


//namespace Fortress.Styles
//{
//    public static class ThemeManager
//    {
//        public static bool UsingLightTheme = true;
//        public static Func<ResourceDictionary> Resources = () => null;

//        public static bool IsThemeDirty = false;

//        public const string Light = "light";
//        public const string Dark = "dark";
//        public const string Black = "black";
//        public const string Nord = "nord";

//        public static void SetThemeStyle(string name, string autoDarkName, ResourceDictionary resources)
//        {
//            try
//            {
//                Resources = () => resources;

//                var newTheme = NeedsThemeUpdate(name, autoDarkName, resources);
//                if (newTheme is null)
//                {
//                    return;
//                }

//                var currentTheme = resources.MergedDictionaries.FirstOrDefault(md => md is IThemeResourceDictionary);
//                if (currentTheme != null)
//                {
//                    resources.MergedDictionaries.Remove(currentTheme);
//                    resources.MergedDictionaries.Add(newTheme);
//                    UsingLightTheme = newTheme is Light;
//                    IsThemeDirty = true;
//                    return;
//                }

//                // Reset styles
//                resources.Clear();
//                resources.MergedDictionaries.Clear();

//                // Variables
//               // resources.MergedDictionaries.Add(new Variables());

//                // Theme
//                resources.MergedDictionaries.Add(newTheme);
//                UsingLightTheme = newTheme is Light;

//                // Base styles
//                resources.MergedDictionaries.Add(new Base());

//                // Platform styles
//                if (DeviceInfo.Platform == DevicePlatform.Android)
//                {
//                    resources.MergedDictionaries.Add(new Fortress.Styles.Android());
//                }
//                else if (DeviceInfo.Platform == DevicePlatform.iOS)
//                {
//                   // resources.MergedDictionaries.Add(new iOS());
//                }
//            }
//            catch (InvalidOperationException ioex) when (ioex.Message != null && ioex.Message.StartsWith("Collection was modified"))
//            {
//                // https://github.com/bitwarden/mobile/issues/1689 There are certain scenarios where this might cause "collection was modified; enumeration operation may not execute"
//                // the way I found to prevent this for now was to catch the exception here and move on.
//                // Because on the screens that I found it to happen, the screen is being closed while trying to apply the resources
//                // so we shouldn't be introducing any issues.
//                // TODO: Maybe something like this https://github.com/matteobortolazzo/HtmlLabelPlugin/pull/113 can be implemented to avoid this
//                // on html labels.
//            }
//            catch 
//            {
//                //LoggerHelper.LogEvenIfCantBeResolved(ex);
//            }
//        }

//        static ResourceDictionary CheckAndGetThemeForMergedDictionaries(Type themeType, ResourceDictionary resources)
//        {
//            return resources.MergedDictionaries.Any(rd => rd.GetType() == themeType)
//                ? null
//                : Activator.CreateInstance(themeType) as ResourceDictionary;
//        }

//        static ResourceDictionary NeedsThemeUpdate(string themeName, string autoDarkThemeName, ResourceDictionary resources)
//        {
//            switch (themeName)
//            {
//                case Dark:
//                    return CheckAndGetThemeForMergedDictionaries(typeof(Dark), resources);
//                case Black:
//                    return CheckAndGetThemeForMergedDictionaries(typeof(Black), resources);
//                case Nord:
//                    return CheckAndGetThemeForMergedDictionaries(typeof(Nord), resources);
//                case Light:
//                    return CheckAndGetThemeForMergedDictionaries(typeof(Light), resources);
//                default:
//                    if (OsDarkModeEnabled())
//                    {
//                        switch (autoDarkThemeName)
//                        {
//                            case Black:
//                                return CheckAndGetThemeForMergedDictionaries(typeof(Black), resources);
//                            case Nord:
//                                return CheckAndGetThemeForMergedDictionaries(typeof(Nord), resources);
//                            default:
//                                return CheckAndGetThemeForMergedDictionaries(typeof(Dark), resources);
//                        }
//                    }
//                    return CheckAndGetThemeForMergedDictionaries(typeof(Light), resources);
//            }
//        } 
//        public static bool OsDarkModeEnabled()
//        {
//            //if (Xamarin.Forms.Application.Current == null)
//            //{
//            //    // called from iOS extension
//            //    var app = new   App( appOptions:   new AppOptions { IosExtension = true });
//            //    return app.RequestedTheme == OSAppTheme.Dark;
//            //}
//            return Application.Current.RequestedTheme == AppTheme.Dark;
//        }

//        public static void ApplyResourcesToPage(ContentPage page)
//        {
//            foreach (var resourceDict in Resources().MergedDictionaries)
//            {
//                page.Resources.Add(resourceDict);
//            }
//        }

//        public static Color GetResourceColor(string color)
//        {
//            return (Color)Resources()[color];
//        }

       
//    }
//}

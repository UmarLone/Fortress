using AuthenticationServices;
using Foundation;

namespace Fortress.iPhone.Autofill
{
    public class Context : AppExtensionContext
    {
        public NSExtensionContext? ExtContext { get; set; }
        public ASCredentialServiceIdentifier[]? ServiceIdentifiers { get; set; }
        public ASPasswordCredentialIdentity? CredentialIdentity { get; set; }
        public bool Configuring { get; set; }
    }

    //public static class ExtensionServices
    //{
    //    private static IServiceProvider _provider;

    //    public static IServiceProvider Provider =>
    //        _provider ??= Build();

    //    private static IServiceProvider Build()
    //    {
    //        //var services = new ServiceCollection();

    //       // services.AddSingleton<CryptographyService>();

    //        return services.BuildServiceProvider();
    //    }

    //    public static T Get<T>() => Provider.GetRequiredService<T>();
    //}
}

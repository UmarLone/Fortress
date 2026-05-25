
namespace Fortress.iPhone.Autofill
{
    [Foundation.Preserve(AllMembers = true)]
    public static class ModuleInitializer
    {
        [Foundation.Preserve]
        public static void Initialize()
        {
            Console.WriteLine("🔧 [AUTOFILL] ModuleInitializer called - Extension module loading");
            System.Diagnostics.Debug.WriteLine("🔧 [AUTOFILL] ModuleInitializer called - Extension module loading");
        }
    }
}

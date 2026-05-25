using Fortress.Services;
namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class NotificationPage : ContentPage, ISecurePage
    {
        public NotificationPage()
        {
            InitializeComponent();
        }
        
    }
    
}
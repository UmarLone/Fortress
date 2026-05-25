
using Fortress.Services;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HelpPage : ContentPage, ISecurePage
    {
        
        public HelpPage()
        {
            InitializeComponent();
        } 
    }
}
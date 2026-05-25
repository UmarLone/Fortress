using Prism.Mvvm;

namespace Fortress.Mobile.Core.Models
{
    public class SecureNoteView : BindableBase
    {
        private string note;
        public string Note
        {
            get { return note; }
            set { SetProperty(ref note, value); }
        }
    }
    public sealed class Software
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string OS { get; set; }
        public string Version { get; set; }
        public string Link { get; set; }
        public string Release { get; set; }
        public string HelpLink { get; set; }
        public string kb { get; set; }

        public string Description { get; set; }
    }

}

using SkiaSharp.Extended.UI.Controls;

namespace Fortress.Models
{
    public class Onboarding:BindableBase
    {
        public string? Title { get; set; }
        public string FileName { get; set; }
        public string? Content { get; set; }

        public object _animationFile;
        public object AnimationFile
        {
            get => _animationFile;
            set => SetProperty(ref _animationFile, value);
        }
       
    }
}

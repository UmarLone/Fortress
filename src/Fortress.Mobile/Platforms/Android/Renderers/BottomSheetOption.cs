namespace Fortress.Droid.Renderers
{
    public class BottomSheetOption
    {
        public string Title { get; set; }
        public int IconResId { get; set; }
        public Func<Task> Action { get; set; }
    }

}
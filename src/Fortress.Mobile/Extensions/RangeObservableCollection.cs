using System.Collections.Specialized;

namespace Fortress.Extensions
{
    public static class ObservableCollectionExtensions
    {
        public static void RemoveWhere<T>(
       this ObservableCollection<T> collection,
       Func<T, bool> predicate)
        {
            var items = collection.Where(predicate).ToList();

            foreach (var item in items)
            {
                collection.Remove(item);
            }
        }
    }
}

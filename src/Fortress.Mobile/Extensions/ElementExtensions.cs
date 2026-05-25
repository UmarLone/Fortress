using System;

namespace Fortress.Extensions
{
    public static class ElementExtensions
    {
        public static T FindParent<T>(this Element element) where T : Element
        {
            Element parent = element;
           
            while (parent != null)
            {
                if (parent is T)
                {
                    return (T)parent;
                }
                parent = parent.Parent;
            }
            return null;
        }
       
    }
}

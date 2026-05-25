using System;
using System.Collections.Generic;
using Fortress.Extensions;
namespace Fortress.Helpers
{
    public static class EnumHelper
    {
        public static Dictionary<int, string> ConvertToDictionary<T>() where T : struct
        {
            var dictionary = new Dictionary<int, string>();

            var values = Enum.GetValues(typeof(T));

            foreach (var value in values)
            {
                int key = (int)value;

                dictionary.Add(key,  (value as Enum).GetDisplayName());
            }

            return dictionary;
        }
    }
}

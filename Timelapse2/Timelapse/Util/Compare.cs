using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Util
{
    // This class compares various objects for differences
    public static class Compare
    {

        // Given two dictionaries, return a dictionary that contains only those key / value pairs in dictionary1 that are not in dictionary2 
        public static Dictionary<string, string> Dictionary1ExceptDictionary2(Dictionary<string, string> dictionary1, Dictionary<string, string> dictionary2)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dictionary1, nameof(dictionary1));
            ThrowIf.IsNullArgument(dictionary2, nameof(dictionary2));

            Dictionary<string, string> dictionaryDifferences = new Dictionary<string, string>();
            List<string> differencesByKeys = dictionary1.Keys.Except(dictionary2.Keys).ToList();
            foreach (string key in differencesByKeys)
            {
                dictionaryDifferences.Add(key, dictionary1[key]);
            }
            return dictionaryDifferences;
        }

        // Given two Lists of strings, return whether they both contain the same values 
        public static bool CompareLists(List<string> list1, List<string> list2)
        {
            if (list1 == null && list2 == null)
            {
                // true as they both contain nothing
                return true;
            }
            if (list1 == null || list2 == null)
            {
                // false as one is null and the other isn't
                return false;
            }

            List<string> firstNotSecond = list1.Except(list2).ToList();
            List<string> secondNotFirst = list2.Except(list1).ToList();
            return !firstNotSecond.Any() && !secondNotFirst.Any();
        }
    }
}

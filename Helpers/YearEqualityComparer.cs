using System;
using System.Collections.Generic;

namespace NavigationMenusMvc.Helpers
{
    class YearEqualityComparer : IEqualityComparer<KeyValuePair<Tuple<int, int>, string>>
    {
        public bool Equals(KeyValuePair<Tuple<int, int>, string> x, KeyValuePair<Tuple<int, int>, string> y)
        {
            return x.Key.Item1 == y.Key.Item1;
        }

        public int GetHashCode(KeyValuePair<Tuple<int, int>, string> obj)
        {
            return obj.Key.Item1.GetHashCode();
        }
    }
}

using System;
using System.Collections.Generic;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    class YearEqualityComparer : IEqualityComparer<KeyValuePair<YearMonthPair, string>>
    {
        public bool Equals(KeyValuePair<YearMonthPair, string> x, KeyValuePair<YearMonthPair, string> y)
        {
            return x.Key.Year == y.Key.Year;
        }

        public int GetHashCode(KeyValuePair<YearMonthPair, string> obj)
        {
            return obj.Key.Year.GetHashCode();
        }
    }
}

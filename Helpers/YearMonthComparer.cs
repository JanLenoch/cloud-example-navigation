using System;
using System.Collections.Generic;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    class YearMonthComparer : IComparer<YearMonthPair>
    {
        public int Compare(YearMonthPair x, YearMonthPair y)
        {
            if (x.Year == y.Year)
            {
                return x.Month.CompareTo(y.Month);
            }
            else
            {
                return 0;
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace NavigationMenusMvc.Helpers
{
    class YearMonthComparer : IComparer<Tuple<int, int>>
    {
        public int Compare(Tuple<int, int> x, Tuple<int, int> y)
        {
            if (x == null)
            {
                throw new ArgumentNullException();
            }

            if (y == null)
            {
                throw new ArgumentNullException();
            }

            if (x.Item1 == y.Item1)
            {
                return x.Item2.CompareTo(y.Item2);
            }
            else
            {
                return x.Item1.CompareTo(y.Item1);
            }
        }
    }
}

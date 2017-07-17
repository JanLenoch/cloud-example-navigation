using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Models
{
    public struct YearMonthPair
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public YearMonthPair(int year, int month)
        {
            Year = year;
            Month = month;
        }
    }
}

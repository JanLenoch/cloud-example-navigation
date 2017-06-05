using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Models
{
    public class PageViewModel
    {
        public NavigationItem Navigation { get; set; }
        public IEnumerable<object> Body { get; set; }
    }
}

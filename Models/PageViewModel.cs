using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    public class PageViewModel
    {
        public INavigationItem Navigation { get; set; }
        public IEnumerable<object> Body { get; set; }
    }
}

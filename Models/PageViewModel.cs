using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    public class PageViewModel
    {
        public NavigationItem Navigation { get; set; }
        public IEnumerable<object> Body { get; set; }
    }
}

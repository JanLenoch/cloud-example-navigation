using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    public class ContentResolverResults
    {
        public bool Found { get; set; }
        public IEnumerable<string> ContentItemCodenames { get; set; }
        public string ViewName { get; set; }
        public string RedirectUrl { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Models
{
    public struct ContentResolverResults : IContentResolverResults
    {
        public bool Found { get; set; }
        public IEnumerable<string> ContentItemCodenames { get; set; }
        public string ViewName { get; set; }
        public string RedirectUrl { get; set; }
    }
}

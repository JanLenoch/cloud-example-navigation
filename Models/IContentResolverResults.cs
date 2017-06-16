using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    public interface IContentResolverResults
    {
        IEnumerable<string> ContentItemCodenames { get; set; }
        bool Found { get; set; }
        string RedirectUrl { get; set; }
        string ViewName { get; set; }
    }
}
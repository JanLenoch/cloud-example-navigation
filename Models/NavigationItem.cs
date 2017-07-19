using KenticoCloud.Delivery;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NavigationMenusMvc.Models
{
    public partial class NavigationItem
    {
        public IEnumerable<NavigationItem> RedirectToItem { get; set; }
        public IEnumerable<NavigationItem> ChildNavigationItems { get; set; }

        // Added on top of generated properties. In-memory properties.
        public string UrlPath { get; set; }
        public string RedirectPath { get; set; }
        public NavigationItem Parent { get; set; }
        public IEnumerable<NavigationItem> AllParents { get; set; }
    }
}
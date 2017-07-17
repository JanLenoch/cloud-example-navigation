using KenticoCloud.Delivery;
using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    // Removed 'partial'.
    public class NavigationItem
    {
        public const string Codename = "navigation_item";
        public const string TitleCodename = "title";
        public const string ContentItemCodename = "content_items";
        public const string RedirectToItemCodename = "local_redirect";
        public const string ChildNavigationItemsCodename = "child_navigation_items";
        public const string RedirectToUrlCodename = "other_redirect";
        public const string UrlSlugCodename = "url_slug";

        public string Title { get; set; }
        public IEnumerable<object> ContentItem { get; set; }

        // Changed from IEnumerable<object> to ease further development.
        public IEnumerable<NavigationItem> RedirectToItem { get; set; }

        // Dtto
        public IEnumerable<NavigationItem> ChildNavigationItems { get; set; }

        public string RedirectToUrl { get; set; }
        public string UrlSlug { get; set; }
        public ContentItemSystemAttributes System { get; set; }

        // Added on top of generated properties. In-memory properties.
        public string UrlPath { get; set; }
        public string RedirectPath { get; set; }
        public NavigationItem Parent { get; set; }
        public IEnumerable<NavigationItem> AllParents { get; set; }
    }
}
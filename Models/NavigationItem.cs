using System;
using System.Collections.Generic;
using KenticoCloud.Delivery;

namespace NavigationMenusMvc.Models
{
    // Removed 'partial'.
    public class NavigationItem
    {
        public const string Codename = "navigation_item";
        public const string ContentItemsCodename = "content_items";
        public const string RedirectCodename = "redirect";
        public const string AppearsInCodename = "appears_in";
        public const string ChildNavigationItemsCodename = "child_navigation_items";
        public const string UrlSlugCodename = "url_slug";
        public const string ViewNameCodename = "view_name";

        public IEnumerable<object> ContentItems { get; set; }

        // Changed from IEnumerable<object>.
        public IEnumerable<NavigationItem> Redirect { get; set; }

        // Dtto
        public IEnumerable<NavigationItem> ChildNavigationItems { get; set; }
        public IEnumerable<MultipleChoiceOption> AppearsIn { get; set; }
        public string UrlSlug { get; set; }
        public string ViewName { get; set; }
        public ContentItemSystemAttributes System { get; set; }

        // Added on top of generated properties.
        public string UrlPath { get; set; }
    }
}
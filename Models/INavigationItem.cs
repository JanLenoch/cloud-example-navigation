using System.Collections.Generic;
using KenticoCloud.Delivery;

namespace NavigationMenusMvc.Models
{
    public interface INavigationItem
    {
        IEnumerable<NavigationItem> AllParents { get; set; }
        IEnumerable<MultipleChoiceOption> AppearsIn { get; set; }
        IEnumerable<NavigationItem> ChildNavigationItems { get; set; }
        IEnumerable<object> ContentItems { get; set; }
        NavigationItem Parent { get; set; }
        string RedirectPath { get; set; }
        IEnumerable<NavigationItem> RedirectToItem { get; set; }
        string RedirectToUrl { get; set; }
        ContentItemSystemAttributes System { get; set; }
        string Title { get; set; }
        string UrlPath { get; set; }
        string UrlSlug { get; set; }
        string ViewName { get; set; }
    }
}
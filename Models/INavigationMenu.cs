using System.Collections.Generic;

namespace NavigationMenusMvc.Models
{
    public interface INavigationMenu
    {
        IEnumerable<NavigationItem> NavigationItems { get; set; }
        string Title { get; set; }
    }
}
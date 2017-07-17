using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface IMenuItemGenerator
    {
        IDictionary<string, Func<NavigationItem, string, Task<NavigationItem>>> StartingUrls { get; }
        Task<NavigationItem> GenerateItemsAsync(NavigationItem item);
    }
}

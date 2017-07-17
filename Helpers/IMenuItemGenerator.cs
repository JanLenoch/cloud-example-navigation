using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public interface IMenuItemGenerator
    {
        IDictionary<string, Func<NavigationItem, string, Task<NavigationItem>>> StartingUrls { get; }
        Task<NavigationItem> GenerateItemsAsync(NavigationItem item);
    }
}

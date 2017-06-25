using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public interface IMenuItemGenerator
    {
        IDictionary<string, Func<INavigationItem, string, Task<INavigationItem>>> WellKnownUrls { get; }
        Task<INavigationItem> TryGenerateItems(INavigationItem item);
    }
}

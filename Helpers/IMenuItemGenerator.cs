using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface IMenuItemGenerator
    {
        Task<NavigationItem> GenerateItemsAsync(NavigationItem item);
    }
}

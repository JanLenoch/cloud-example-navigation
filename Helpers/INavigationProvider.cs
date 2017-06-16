using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface INavigationProvider
    {
        Task<NavigationItem> GetNavigationItemsAsync();
        Task<NavigationItem> GetOrCreateCachedNavigationAsync();
    }
}